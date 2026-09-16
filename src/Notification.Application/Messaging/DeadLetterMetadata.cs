namespace Notification.Application.Messaging;

public sealed class DeadLetterMetadata
{
    public Guid NotificationId { get; init; }
    public string? OriginalMessageId { get; init; }
    public string FailureReason { get; init; } = default!;
    public int RetryCount { get; init; }
    public DateTime FailedAtUtc { get; init; }
    public string ExceptionType { get; init; } = default!;
    public string FailureKind { get; init; } = default!;
}
