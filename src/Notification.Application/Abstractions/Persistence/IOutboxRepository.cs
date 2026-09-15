using Notification.Domain.Entities;

namespace Notification.Application.Abstractions.Persistence;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize,
        TimeSpan lockDuration,
        CancellationToken cancellationToken);

    Task<int> CountPendingAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
