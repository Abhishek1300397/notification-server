using Notification.Domain.Enums;

namespace Notification.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? NotificationId { get; set; }
    public string Type { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public Guid? LockId { get; set; }

    public void MarkPublished(DateTime utcNow)
    {
        Status = OutboxStatus.Published;
        ProcessedAtUtc = utcNow;
        LockedUntilUtc = null;
        LockId = null;
        LastError = null;
    }

    public void MarkPendingRetry(DateTime utcNow, DateTime nextAttemptAtUtc, string error)
    {
        Status = OutboxStatus.Pending;
        RetryCount++;
        LastError = Truncate(error);
        NextAttemptAtUtc = nextAttemptAtUtc;
        LockedUntilUtc = null;
        LockId = null;
        _ = utcNow;
    }

    public void MarkFailed(DateTime utcNow, string error)
    {
        Status = OutboxStatus.Failed;
        RetryCount++;
        LastError = Truncate(error);
        FailedAtUtc = utcNow;
        LockedUntilUtc = null;
        LockId = null;
    }

    private static string Truncate(string error) =>
        error.Length <= 2000 ? error : error[..2000];
}
