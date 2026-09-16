using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Messaging;
using Notification.Application.Abstractions.Notifications;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Processing;
using Notification.Application.Configuration;
using Notification.Application.Messaging;
using Notification.Domain.Exceptions;
using Notification.Infrastructure.Messaging.RabbitMq;
using Notification.Infrastructure.Observability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Notification.Infrastructure.BackgroundJobs;

public sealed class NotificationConsumerHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRabbitMqConnectionManager _connections;
    private readonly RabbitMqTopology _topology;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRetryBackoffCalculator _backoff;
    private readonly INotificationMetrics _metrics;
    private readonly WorkerHealthState _health;
    private readonly RabbitMqOptions _options;
    private readonly NotificationRetryOptions _retryOptions;
    private readonly ILogger<NotificationConsumerHostedService> _logger;

    public NotificationConsumerHostedService(
        IRabbitMqConnectionManager connections,
        RabbitMqTopology topology,
        IServiceScopeFactory scopeFactory,
        IRetryBackoffCalculator backoff,
        INotificationMetrics metrics,
        WorkerHealthState health,
        IOptions<RabbitMqOptions> options,
        IOptions<NotificationRetryOptions> retryOptions,
        ILogger<NotificationConsumerHostedService> logger)
    {
        _connections = connections;
        _topology = topology;
        _scopeFactory = scopeFactory;
        _backoff = backoff;
        _metrics = metrics;
        _health = health;
        _options = options.Value;
        _retryOptions = retryOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _health.SetConsumerRunning(true);
        _logger.LogInformation("Notification consumer starting");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ConsumeUntilDisconnectedAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _metrics.SetRabbitMqConnected(false);
                    _logger.LogWarning(ex, "RabbitMQ consumer loop failed. Reconnecting");
                    await DelayReconnectAsync(stoppingToken);
                }
            }
        }
        finally
        {
            _health.SetConsumerRunning(false);
            _logger.LogInformation("Notification consumer stopped");
        }
    }

    private async Task ConsumeUntilDisconnectedAsync(CancellationToken stoppingToken)
    {
        var connection = await _connections.GetConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _topology.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(0, _options.PrefetchCount, global: false, cancellationToken: stoppingToken);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = stoppingToken.Register(() => tcs.TrySetCanceled(stoppingToken));

        connection.ConnectionShutdownAsync += (_, _) =>
        {
            tcs.TrySetException(new IOException("RabbitMQ connection shut down."));
            return Task.CompletedTask;
        };

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                await HandleMessageAsync(channel, args, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled consumer error for delivery {DeliveryTag}", args.DeliveryTag);
            }
        };

        await channel.BasicConsumeAsync(
            _options.Queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Consuming queue {Queue} with prefetch {PrefetchCount}", _options.Queue, _options.PrefetchCount);
        await tcs.Task;
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        CancellationToken stoppingToken)
    {
        _metrics.RecordMessageConsumed();
        _health.MarkConsumerMessage();

        NotificationMessage? message = null;
        var retryCount = ReadRetryCount(args);

        try
        {
            message = Deserialize(args.Body.ToArray());

            await using var scope = _scopeFactory.CreateAsyncScope();
            var validator = scope.ServiceProvider.GetRequiredService<INotificationMessageValidator>();
            var processor = scope.ServiceProvider.GetRequiredService<INotificationProcessor>();

            validator.Validate(message);

            _logger.LogInformation(
                "Processing notification {NotificationId} from RabbitMQ retry {RetryCount}",
                message.NotificationId,
                retryCount);

            await processor.ProcessAsync(message, stoppingToken);

            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            _metrics.RecordMessageAcknowledged();
        }
        catch (PermanentNotificationException ex)
        {
            await SendToDeadLetterAsync(channel, args, message, retryCount, ex, stoppingToken);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            _metrics.RecordMessageAcknowledged();
        }
        catch (TransientNotificationException ex)
        {
            await RetryOrDeadLetterAsync(channel, args, message, retryCount, ex, stoppingToken);
        }
        catch (JsonException ex)
        {
            await SendToDeadLetterAsync(channel, args, message, retryCount, ex, stoppingToken);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            _metrics.RecordMessageAcknowledged();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            if (!channel.IsClosed)
            {
                await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, cancellationToken: CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process notification {NotificationId}",
                message?.NotificationId);
            await RetryOrDeadLetterAsync(channel, args, message, retryCount, ex, stoppingToken);
        }
    }

    private async Task RetryOrDeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        NotificationMessage? message,
        int retryCount,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var nextAttempt = retryCount + 1;
        if (nextAttempt >= _retryOptions.MaxAttempts)
        {
            await SendToDeadLetterAsync(channel, args, message, retryCount, exception, cancellationToken);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
            _metrics.RecordMessageAcknowledged();
            return;
        }

        var delay = _backoff.GetDelay(nextAttempt);
        var delaySeconds = Math.Max(1, (int)delay.TotalSeconds);
        var available = _topology.DistinctRetryDelays();
        var selectedDelay = available.Count == 0
            ? delaySeconds
            : available.OrderBy(d => d).FirstOrDefault(d => d >= delaySeconds, available[^1]);

        var headers = CopyHeaders(args);
        headers["x-retry-count"] = nextAttempt;
        headers["x-notification-id"] = message?.NotificationId.ToString() ?? string.Empty;
        headers["x-last-error"] = Sanitize(exception.Message);

        await channel.BasicPublishAsync(
            exchange: _options.RetryExchange,
            routingKey: _topology.RetryRoutingKey(selectedDelay),
            mandatory: true,
            basicProperties: new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = args.BasicProperties.MessageId,
                Headers = headers
            },
            body: args.Body,
            cancellationToken: cancellationToken);

        await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
        _metrics.RecordMessageRetried();
        _metrics.RecordMessageAcknowledged();

        _logger.LogWarning(
            "Notification {NotificationId} failed. Retry attempt {RetryAttempt} delayed by {DelaySeconds}s",
            message?.NotificationId,
            nextAttempt,
            selectedDelay);
    }

    private async Task SendToDeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        NotificationMessage? message,
        int retryCount,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var metadata = new DeadLetterMetadata
        {
            NotificationId = message?.NotificationId ?? Guid.Empty,
            OriginalMessageId = args.BasicProperties.MessageId,
            FailureReason = Sanitize(exception.Message),
            RetryCount = retryCount,
            FailedAtUtc = DateTime.UtcNow,
            ExceptionType = exception.GetType().Name,
            FailureKind = exception is PermanentNotificationException ? "Permanent" : "Transient"
        };

        var headers = CopyHeaders(args);
        headers["x-notification-id"] = metadata.NotificationId.ToString();
        headers["x-original-message-id"] = metadata.OriginalMessageId ?? string.Empty;
        headers["x-failure-reason"] = metadata.FailureReason;
        headers["x-retry-count"] = metadata.RetryCount;
        headers["x-failed-at-utc"] = metadata.FailedAtUtc.ToString("O");
        headers["x-exception-type"] = metadata.ExceptionType;
        headers["x-failure-kind"] = metadata.FailureKind;

        await channel.BasicPublishAsync(
            exchange: _options.DeadLetterExchange,
            routingKey: _options.DeadLetterRoutingKey,
            mandatory: true,
            basicProperties: new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = args.BasicProperties.MessageId,
                Headers = headers
            },
            body: args.Body,
            cancellationToken: cancellationToken);

        _metrics.RecordMessageSentToDlq();
        _logger.LogError(
            exception,
            "Sent notification {NotificationId} to DLQ after {RetryCount} retries",
            metadata.NotificationId,
            retryCount);
    }

    private static NotificationMessage Deserialize(byte[] body)
    {
        var json = Encoding.UTF8.GetString(body);
        return JsonSerializer.Deserialize<NotificationMessage>(json, JsonOptions)
               ?? throw new JsonException("Notification message deserialized to null.");
    }

    private static int ReadRetryCount(BasicDeliverEventArgs args)
    {
        if (args.BasicProperties.Headers is null)
        {
            return 0;
        }

        if (!args.BasicProperties.Headers.TryGetValue("x-retry-count", out var raw) || raw is null)
        {
            return 0;
        }

        return raw switch
        {
            int i => i,
            long l => (int)l,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => 0
        };
    }

    private static Dictionary<string, object?> CopyHeaders(BasicDeliverEventArgs args)
    {
        var headers = new Dictionary<string, object?>();
        if (args.BasicProperties.Headers is null)
        {
            return headers;
        }

        foreach (var (key, value) in args.BasicProperties.Headers)
        {
            headers[key] = value;
        }

        return headers;
    }

    private static string Sanitize(string value)
    {
        var trimmed = value.ReplaceLineEndings(" ").Trim();
        return trimmed.Length <= 256 ? trimmed : trimmed[..256];
    }

    private static async Task DelayReconnectAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
