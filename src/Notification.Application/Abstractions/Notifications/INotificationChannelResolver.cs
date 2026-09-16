using Notification.Domain.Enums;

namespace Notification.Application.Abstractions.Notifications;

public interface INotificationChannelResolver
{
    INotificationChannel Resolve(NotificationChannel channel);
}
