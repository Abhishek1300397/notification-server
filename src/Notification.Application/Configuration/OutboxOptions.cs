namespace Notification.Application.Configuration;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 100;
    public int PollingIntervalSeconds { get; set; } = 5;
    public int MaxRetryCount { get; set; } = 5;
    public int LockDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Parallel outbox pollers in this process. Safe with FOR UPDATE SKIP LOCKED.
    /// Increase this, BatchSize, or run more app instances to raise throughput.
    /// </summary>
    public int WorkerCount { get; set; } = 1;
}
