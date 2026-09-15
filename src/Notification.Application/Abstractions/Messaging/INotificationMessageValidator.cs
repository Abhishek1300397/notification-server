using Notification.Application.Messaging;

namespace Notification.Application.Abstractions.Messaging;

public interface INotificationMessageValidator
{
    void Validate(NotificationMessage? message);
}
