using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions.Notifications;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Infrastructure.Notifications.Channels;

public sealed class SmsNotificationChannel : INotificationChannel
{
    private readonly ILogger<SmsNotificationChannel> _logger;

    public SmsNotificationChannel(ILogger<SmsNotificationChannel> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Sms;

    public Task SendAsync(
        NotificationRequest notification,
        RenderedTemplate renderedTemplate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "Fake SMS channel accepted notification {NotificationId} with body length {BodyLength}",
            notification.Id,
            renderedTemplate.Body.Length);
        return Task.CompletedTask;
    }
}
