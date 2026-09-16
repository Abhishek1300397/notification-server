using Notification.Application.Abstractions.Messaging;
using Notification.Application.Messaging;
using Notification.Domain.Exceptions;

namespace Notification.Application.Messaging;

public sealed class NotificationMessageValidator : INotificationMessageValidator
{
    public void Validate(NotificationMessage? message)
    {
        if (message is null)
        {
            throw new PermanentNotificationException("Notification message is empty.");
        }

        if (message.NotificationId == Guid.Empty)
        {
            throw new PermanentNotificationException("NotificationId is required.");
        }

        if (message.TenantId == Guid.Empty)
        {
            throw new PermanentNotificationException("TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(message.Channel))
        {
            throw new PermanentNotificationException("Channel is required.");
        }

        if (string.IsNullOrWhiteSpace(message.TemplateId))
        {
            throw new PermanentNotificationException("TemplateId is required.");
        }
    }
}
