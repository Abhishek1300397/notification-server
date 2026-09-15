using Notification.Application.Messaging;

namespace Notification.Application.Abstractions.Processing;

public interface IOutboxPublisher
{
    Task ProcessBatchAsync(CancellationToken cancellationToken);
}
