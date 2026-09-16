using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using RabbitMQ.Client;

namespace Notification.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqTopology
{
    private readonly RabbitMqOptions _options;
    private readonly Notification.Application.Configuration.NotificationRetryOptions _retry;
    private readonly ILogger<RabbitMqTopology> _logger;

    public RabbitMqTopology(
        IOptions<RabbitMqOptions> options,
        IOptions<Notification.Application.Configuration.NotificationRetryOptions> retry,
        ILogger<RabbitMqTopology> logger)
    {
        _options = options.Value;
        _retry = retry.Value;
        _logger = logger;
    }

    public async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            _options.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            _options.DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            _options.RetryExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var queueArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = _options.DeadLetterRoutingKey,
            ["x-max-priority"] = (int)_options.MaxPriority
        };

        await channel.QueueDeclareAsync(
            _options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            _options.Queue,
            _options.Exchange,
            _options.RoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            _options.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            _options.DeadLetterQueue,
            _options.DeadLetterExchange,
            _options.DeadLetterRoutingKey,
            cancellationToken: cancellationToken);

        foreach (var delaySeconds in DistinctRetryDelays())
        {
            var queueName = RetryQueueName(delaySeconds);
            var routingKey = RetryRoutingKey(delaySeconds);
            var args = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = delaySeconds * 1000,
                ["x-dead-letter-exchange"] = _options.Exchange,
                ["x-dead-letter-routing-key"] = _options.RoutingKey,
                ["x-max-priority"] = (int)_options.MaxPriority
            };

            await channel.QueueDeclareAsync(
                queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: args,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queueName,
                _options.RetryExchange,
                routingKey,
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "Declared RabbitMQ topology. Exchange={Exchange} Queue={Queue} Dlx={Dlx} Dlq={Dlq}",
            _options.Exchange,
            _options.Queue,
            _options.DeadLetterExchange,
            _options.DeadLetterQueue);
    }

    public string RetryQueueName(int delaySeconds) => $"{_options.Queue}.retry.{delaySeconds}s";

    public string RetryRoutingKey(int delaySeconds) => $"retry.{delaySeconds}";

    public IReadOnlyList<int> DistinctRetryDelays()
    {
        var delays = _retry.DelaySeconds is { Length: > 0 }
            ? _retry.DelaySeconds
            : [_retry.InitialDelaySeconds];

        return delays
            .Where(d => d > 0)
            .Distinct()
            .ToArray();
    }
}
