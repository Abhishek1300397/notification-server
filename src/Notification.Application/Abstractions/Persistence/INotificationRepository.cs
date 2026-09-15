using Notification.Domain.Entities;

namespace Notification.Application.Abstractions.Persistence;

public interface INotificationRepository
{
    Task AddAsync(NotificationRequest notification, CancellationToken cancellationToken);

    Task<NotificationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<NotificationRequest?> GetByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<bool> TryClaimForProcessingAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
