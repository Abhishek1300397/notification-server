using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Notifications;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Persistence;
using Notification.Application.Abstractions.Time;
using Notification.Application.Configuration;
using Notification.Application.Messaging;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Domain.Exceptions;

namespace Notification.Application.Notifications;

public sealed class NotificationProcessor : INotificationProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly INotificationRepository _notifications;
    private readonly ITemplateRepository _templates;
    private readonly ITemplateRenderer _renderer;
    private readonly INotificationChannelResolver _channels;
    private readonly IClock _clock;
    private readonly INotificationMetrics _metrics;
    private readonly NotificationRetryOptions _retryOptions;
    private readonly ILogger<NotificationProcessor> _logger;

    public NotificationProcessor(
        INotificationRepository notifications,
        ITemplateRepository templates,
        ITemplateRenderer renderer,
        INotificationChannelResolver channels,
        IClock clock,
        INotificationMetrics metrics,
        IOptions<NotificationRetryOptions> retryOptions,
        ILogger<NotificationProcessor> logger)
    {
        _notifications = notifications;
        _templates = templates;
        _renderer = renderer;
        _channels = channels;
        _clock = clock;
        _metrics = metrics;
        _retryOptions = retryOptions.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var started = _clock.UtcNow;

        var notification = await _notifications.GetByIdAsync(message.NotificationId, cancellationToken);
        if (notification is null)
        {
            throw new PermanentNotificationException(
                $"Notification {message.NotificationId} was not found.");
        }

        if (notification.TenantId != message.TenantId)
        {
            throw new PermanentNotificationException(
                $"Notification {message.NotificationId} does not belong to tenant {message.TenantId}.");
        }

        if (notification.IsTerminalSuccess)
        {
            _logger.LogInformation(
                "Skipping already sent notification {NotificationId}",
                notification.Id);
            return;
        }

        if (notification.Status == NotificationStatus.Failed)
        {
            _logger.LogInformation(
                "Skipping permanently failed notification {NotificationId}",
                notification.Id);
            return;
        }

        var claimed = await _notifications.TryClaimForProcessingAsync(notification.Id, cancellationToken);
        if (!claimed)
        {
            var current = await _notifications.GetByIdAsync(notification.Id, cancellationToken);
            if (current?.IsTerminalSuccess == true || current?.Status == NotificationStatus.Failed)
            {
                _logger.LogInformation(
                    "Notification {NotificationId} already reached a terminal state; acknowledging duplicate",
                    notification.Id);
                return;
            }

            throw new TransientNotificationException(
                $"Notification {notification.Id} is already being processed.");
        }

        notification = await _notifications.GetByIdAsync(notification.Id, cancellationToken)
                       ?? throw new PermanentNotificationException(
                           $"Notification {message.NotificationId} was not found after claim.");

        try
        {
            var template = await _templates.GetAsync(
                notification.TenantId,
                notification.TemplateId,
                notification.Channel,
                cancellationToken);

            if (template is null || !template.IsActive)
            {
                throw new PermanentNotificationException(
                    $"Template {notification.TemplateId} is not available for tenant {notification.TenantId}.");
            }

            var data = DeserializeData(notification.DataJson);
            var rendered = _renderer.Render(template, data);
            var channel = _channels.Resolve(notification.Channel);

            _logger.LogInformation(
                "Sending notification {NotificationId} via {Channel}",
                notification.Id,
                notification.Channel);

            await channel.SendAsync(notification, rendered, cancellationToken);

            notification.MarkSent(_clock.UtcNow);
            await _notifications.SaveChangesAsync(cancellationToken);

            _metrics.RecordNotificationSent();
            _metrics.RecordProcessingDuration(_clock.UtcNow - started);

            _logger.LogInformation(
                "Notification {NotificationId} sent successfully",
                notification.Id);
        }
        catch (PermanentNotificationException ex)
        {
            await FailAsync(notification, ex.Message, cancellationToken);
            throw;
        }
        catch (TransientNotificationException ex)
        {
            await HandleTransientFailureAsync(notification, ex.Message, cancellationToken);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            notification.Status = NotificationStatus.Retrying;
            notification.UpdatedAtUtc = _clock.UtcNow;
            await _notifications.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure processing notification {NotificationId}", notification.Id);
            await HandleTransientFailureAsync(notification, ex.GetType().Name, cancellationToken);
            throw new TransientNotificationException("Unexpected notification processing failure.", ex);
        }
    }

    private async Task HandleTransientFailureAsync(
        NotificationRequest notification,
        string error,
        CancellationToken cancellationToken)
    {
        if (notification.AttemptCount + 1 >= _retryOptions.MaxAttempts)
        {
            await FailAsync(notification, error, cancellationToken);
            throw new PermanentNotificationException(
                $"Retry limit reached for notification {notification.Id}.");
        }

        notification.MarkRetrying(_clock.UtcNow, error);
        await _notifications.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Notification {NotificationId} failed. Retry attempt {RetryAttempt}",
            notification.Id,
            notification.AttemptCount);
    }

    private async Task FailAsync(
        NotificationRequest notification,
        string error,
        CancellationToken cancellationToken)
    {
        notification.MarkFailed(_clock.UtcNow, error);
        await _notifications.SaveChangesAsync(cancellationToken);
        _metrics.RecordNotificationFailed();

        _logger.LogError(
            "Notification {NotificationId} marked as failed",
            notification.Id);
    }

    private static IReadOnlyDictionary<string, string> DeserializeData(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException ex)
        {
            throw new PermanentNotificationException("Notification payload is invalid.", ex);
        }
    }
}
