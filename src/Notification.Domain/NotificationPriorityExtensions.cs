using Notification.Domain.Enums;

namespace Notification.Domain;

public static class NotificationPriorityExtensions
{
    public static byte ToRabbitMqPriority(this NotificationPriority priority) =>
        (byte)Math.Clamp((int)priority, 0, 9);
}
