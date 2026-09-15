namespace Notification.Infrastructure.Observability;

public sealed class WorkerHealthState
{
    private volatile bool _outboxLoopRunning;
    private volatile bool _consumerRunning;
    private DateTime _outboxLastSuccessfulPollUtc = DateTime.UtcNow;
    private DateTime _consumerLastMessageUtc = DateTime.UtcNow;

    public bool OutboxLoopRunning => _outboxLoopRunning;
    public bool ConsumerRunning => _consumerRunning;
    public DateTime OutboxLastSuccessfulPollUtc => _outboxLastSuccessfulPollUtc;
    public DateTime ConsumerLastMessageUtc => _consumerLastMessageUtc;

    public void SetOutboxRunning(bool running) => _outboxLoopRunning = running;
    public void SetConsumerRunning(bool running) => _consumerRunning = running;
    public void MarkOutboxPoll() => _outboxLastSuccessfulPollUtc = DateTime.UtcNow;
    public void MarkConsumerMessage() => _consumerLastMessageUtc = DateTime.UtcNow;
}
