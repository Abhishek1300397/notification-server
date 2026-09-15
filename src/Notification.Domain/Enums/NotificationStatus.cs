namespace Notification.Domain.Enums;

public enum NotificationStatus
{
    Pending = 1,
    Processing = 2,
    Sent = 3,
    Retrying = 4,
    Failed = 5
}
