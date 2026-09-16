using Microsoft.EntityFrameworkCore;
using Notification.Application.Abstractions.Persistence;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _db;

    public NotificationRepository(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(NotificationRequest notification, CancellationToken cancellationToken)
    {
        await _db.Notifications.AddAsync(notification, cancellationToken);
    }

    public Task<NotificationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Notifications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<NotificationRequest?> GetByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        _db.Notifications.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public async Task<bool> TryClaimForProcessingAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var pending = NotificationStatus.Pending.ToString();
        var retrying = NotificationStatus.Retrying.ToString();
        var processing = NotificationStatus.Processing.ToString();

        var updated = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE notifications
             SET status = {processing},
                 updated_at_utc = {now}
             WHERE id = {id}
               AND status IN ({pending}, {retrying})
             """,
            cancellationToken);

        if (updated == 1)
        {
            var local = _db.Notifications.Local.FirstOrDefault(x => x.Id == id);
            if (local is not null)
            {
                await _db.Entry(local).ReloadAsync(cancellationToken);
            }
        }

        return updated == 1;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
