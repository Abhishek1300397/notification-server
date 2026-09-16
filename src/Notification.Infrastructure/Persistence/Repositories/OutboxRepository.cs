using Microsoft.EntityFrameworkCore;
using Notification.Application.Abstractions.Persistence;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Infrastructure.Persistence.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly NotificationDbContext _db;

    public OutboxRepository(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        await _db.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        var lockId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var lockUntil = now.Add(lockDuration);
        var pending = OutboxStatus.Pending.ToString();
        var processing = OutboxStatus.Processing.ToString();

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE outbox_messages AS o
             SET status = {processing},
                 lock_id = {lockId},
                 locked_until_utc = {lockUntil}
             WHERE o.id IN (
                 SELECT id
                 FROM outbox_messages
                 WHERE next_attempt_at_utc <= {now}
                   AND (
                       status = {pending}
                       OR (status = {processing} AND locked_until_utc < {now})
                   )
                 ORDER BY priority DESC, created_at_utc
                 LIMIT {batchSize}
                 FOR UPDATE SKIP LOCKED
             )
             """,
            cancellationToken);

        var claimed = await _db.OutboxMessages
            .Where(x => x.LockId == lockId)
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    public Task<int> CountPendingAsync(CancellationToken cancellationToken) =>
        _db.OutboxMessages
            .AsNoTracking()
            .CountAsync(x => x.Status == OutboxStatus.Pending, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
