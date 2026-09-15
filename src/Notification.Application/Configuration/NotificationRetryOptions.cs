namespace Notification.Application.Configuration;

public sealed class NotificationRetryOptions
{
    public const string SectionName = "NotificationRetry";

    public int MaxAttempts { get; set; } = 5;
    public int InitialDelaySeconds { get; set; } = 5;
    public int MaxDelaySeconds { get; set; } = 600;

    /// <summary>
    /// Optional explicit delay ladder in seconds after each failed attempt.
    /// Example: [5, 30, 120, 600] means wait 5s after attempt 1, 30s after attempt 2, etc.
    /// When empty, exponential backoff from <see cref="InitialDelaySeconds"/> is used.
    /// </summary>
    public int[] DelaySeconds { get; set; } = [5, 30, 120, 600];
}
