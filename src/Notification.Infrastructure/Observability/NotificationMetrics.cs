using System.Diagnostics.Metrics;
using Notification.Application.Abstractions.Observability;

namespace Notification.Infrastructure.Observability;

public sealed class NotificationMetrics : INotificationMetrics
{
    public const string MeterName = "Notification.Manager";

    private readonly Counter<long> _outboxPublishSuccess;
    private readonly Counter<long> _outboxPublishFailure;
    private readonly Counter<long> _consumed;
    private readonly Counter<long> _acked;
    private readonly Counter<long> _retried;
    private readonly Counter<long> _dlq;
    private readonly Counter<long> _sent;
    private readonly Counter<long> _failed;
    private readonly Histogram<double> _durationMs;
    private long _pending;
    private int _rabbitConnected;

    public NotificationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _outboxPublishSuccess = meter.CreateCounter<long>("notification.outbox.publish.success");
        _outboxPublishFailure = meter.CreateCounter<long>("notification.outbox.publish.failure");
        _consumed = meter.CreateCounter<long>("notification.messages.consumed");
        _acked = meter.CreateCounter<long>("notification.messages.acknowledged");
        _retried = meter.CreateCounter<long>("notification.messages.retried");
        _dlq = meter.CreateCounter<long>("notification.messages.dlq");
        _sent = meter.CreateCounter<long>("notification.sent");
        _failed = meter.CreateCounter<long>("notification.failed");
        _durationMs = meter.CreateHistogram<double>("notification.processing.duration", unit: "ms");

        meter.CreateObservableGauge("notification.outbox.pending", () => Volatile.Read(ref _pending));
        meter.CreateObservableGauge("notification.rabbitmq.connected", () => Volatile.Read(ref _rabbitConnected));
    }

    public void RecordOutboxPending(int count) => Volatile.Write(ref _pending, count);
    public void RecordOutboxPublishSuccess(int count) => _outboxPublishSuccess.Add(count);
    public void RecordOutboxPublishFailure(int count) => _outboxPublishFailure.Add(count);
    public void RecordMessageConsumed() => _consumed.Add(1);
    public void RecordMessageAcknowledged() => _acked.Add(1);
    public void RecordMessageRetried() => _retried.Add(1);
    public void RecordMessageSentToDlq() => _dlq.Add(1);
    public void RecordNotificationSent() => _sent.Add(1);
    public void RecordNotificationFailed() => _failed.Add(1);
    public void RecordProcessingDuration(TimeSpan duration) => _durationMs.Record(duration.TotalMilliseconds);
    public void SetRabbitMqConnected(bool connected) => Volatile.Write(ref _rabbitConnected, connected ? 1 : 0);
}
