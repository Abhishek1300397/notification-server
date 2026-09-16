namespace Notification.Application.Abstractions.Observability;

public interface INotificationMetrics
{
    void RecordOutboxPending(int count);
    void RecordOutboxPublishSuccess(int count);
    void RecordOutboxPublishFailure(int count);
    void RecordMessageConsumed();
    void RecordMessageAcknowledged();
    void RecordMessageRetried();
    void RecordMessageSentToDlq();
    void RecordNotificationSent();
    void RecordNotificationFailed();
    void RecordProcessingDuration(TimeSpan duration);
    void SetRabbitMqConnected(bool connected);
}
