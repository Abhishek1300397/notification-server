using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions.Notifications;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Infrastructure.Notifications.Channels;

public sealed class PushNotificationChannel : INotificationChannel
{
    private readonly ILogger<PushNotificationChannel> _logger;

    public PushNotificationChannel(ILogger<PushNotificationChannel> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Push;

    public Task SendAsync(
        NotificationRequest notification,
        RenderedTemplate renderedTemplate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "Fake push channel accepted notification {NotificationId}",
            notification.Id);
        return Task.CompletedTask;
    }
}
