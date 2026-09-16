using Microsoft.EntityFrameworkCore;
using Notification.Application.Abstractions.Persistence;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Infrastructure.Persistence.Repositories;

public sealed class TemplateRepository : ITemplateRepository
{
    private readonly NotificationDbContext _db;

    public TemplateRepository(NotificationDbContext db)
    {
        _db = db;
    }

    public Task<NotificationTemplate?> GetAsync(
        Guid tenantId,
        string templateId,
        NotificationChannel channel,
        CancellationToken cancellationToken) =>
        _db.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == templateId && x.Channel == channel,
                cancellationToken);
}
