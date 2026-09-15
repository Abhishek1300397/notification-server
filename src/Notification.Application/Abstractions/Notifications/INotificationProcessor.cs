using Notification.Application.Messaging;

namespace Notification.Application.Abstractions.Notifications;

public interface INotificationProcessor
{
    Task ProcessAsync(NotificationMessage message, CancellationToken cancellationToken);
}
