namespace Notification.Domain.Enums;

public enum OutboxStatus
{
    Pending = 1,
    Processing = 2,
    Published = 3,
    Failed = 4
}
