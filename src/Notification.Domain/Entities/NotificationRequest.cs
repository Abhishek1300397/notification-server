using Notification.Domain.Enums;

namespace Notification.Domain.Entities;

public sealed class NotificationRequest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string Recipient { get; set; } = default!;
    public string TemplateId { get; set; } = default!;
    public string DataJson { get; set; } = "{}";
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }

    public bool IsTerminalSuccess => Status == NotificationStatus.Sent;

    public bool CanStartProcessing =>
        Status is NotificationStatus.Pending or NotificationStatus.Retrying;

    public void MarkProcessing(DateTime utcNow)
    {
        Status = NotificationStatus.Processing;
        UpdatedAtUtc = utcNow;
    }

    public void MarkSent(DateTime utcNow)
    {
        Status = NotificationStatus.Sent;
        SentAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
        LastError = null;
    }

    public void MarkRetrying(DateTime utcNow, string error)
    {
        Status = NotificationStatus.Retrying;
        AttemptCount++;
        LastError = Truncate(error);
        UpdatedAtUtc = utcNow;
    }

    public void MarkFailed(DateTime utcNow, string error)
    {
        Status = NotificationStatus.Failed;
        AttemptCount++;
        LastError = Truncate(error);
        FailedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    private static string Truncate(string error) =>
        error.Length <= 2000 ? error : error[..2000];
}
