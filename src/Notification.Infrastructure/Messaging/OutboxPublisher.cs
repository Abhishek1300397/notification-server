using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Messaging;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Persistence;
using Notification.Application.Abstractions.Processing;
using Notification.Application.Abstractions.Time;
using Notification.Application.Configuration;
using Notification.Domain;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Messaging;

public sealed class OutboxPublisher : IOutboxPublisher
{
    private readonly IOutboxRepository _outbox;
    private readonly IMessagePublisher _publisher;
    private readonly IRetryBackoffCalculator _backoff;
    private readonly IClock _clock;
    private readonly INotificationMetrics _metrics;
    private readonly OutboxOptions _outboxOptions;
    private readonly RabbitMqOptions _rabbitMq;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(
        IOutboxRepository outbox,
        IMessagePublisher publisher,
        IRetryBackoffCalculator backoff,
        IClock clock,
        INotificationMetrics metrics,
        IOptions<OutboxOptions> outboxOptions,
        IOptions<RabbitMqOptions> rabbitMq,
        ILogger<OutboxPublisher> logger)
    {
        _outbox = outbox;
        _publisher = publisher;
        _backoff = backoff;
        _clock = clock;
        _metrics = metrics;
        _outboxOptions = outboxOptions.Value;
        _rabbitMq = rabbitMq.Value;
        _logger = logger;
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var pending = await _outbox.CountPendingAsync(cancellationToken);
        _metrics.RecordOutboxPending(pending);

        var claimed = await _outbox.ClaimPendingAsync(
            _outboxOptions.BatchSize,
            TimeSpan.FromSeconds(_outboxOptions.LockDurationSeconds),
            cancellationToken);

        if (claimed.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Claimed {Count} outbox messages for publishing", claimed.Count);

        var published = 0;
        var failed = 0;

        foreach (var message in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var headers = new Dictionary<string, object>
                {
                    ["x-outbox-id"] = message.Id.ToString(),
                    ["x-tenant-id"] = message.TenantId.ToString(),
                    ["x-notification-id"] = message.NotificationId?.ToString() ?? string.Empty,
                    ["x-retry-count"] = 0,
                    ["x-priority"] = message.Priority.ToString()
                };

                await _publisher.PublishJsonAsync(
                    _rabbitMq.Exchange,
                    _rabbitMq.RoutingKey,
                    message.Payload,
                    headers,
                    message.Priority.ToRabbitMqPriority(),
                    cancellationToken);

                message.MarkPublished(_clock.UtcNow);
                published++;

                _logger.LogInformation(
                    "Published outbox message {OutboxId} for notification {NotificationId}",
                    message.Id,
                    message.NotificationId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                HandlePublishFailure(message, ex);
            }
        }

        await _outbox.SaveChangesAsync(cancellationToken);
        _metrics.RecordOutboxPublishSuccess(published);
        _metrics.RecordOutboxPublishFailure(failed);
    }

    private void HandlePublishFailure(OutboxMessage message, Exception ex)
    {
        var error = ex.GetType().Name;
        if (message.RetryCount + 1 >= _outboxOptions.MaxRetryCount)
        {
            message.MarkFailed(_clock.UtcNow, error);
            _logger.LogError(
                ex,
                "Outbox message {OutboxId} exhausted retries and was marked failed",
                message.Id);
            return;
        }

        var nextAttempt = _backoff.GetNextAttemptUtc(message.RetryCount + 1, _clock.UtcNow);
        message.MarkPendingRetry(_clock.UtcNow, nextAttempt, error);
        _logger.LogWarning(
            ex,
            "Failed to publish outbox message {OutboxId}. Next attempt at {NextAttemptAtUtc}",
            message.Id,
            nextAttempt);
    }
}
