using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Application.Abstractions.Persistence;

public interface ITemplateRepository
{
    Task<NotificationTemplate?> GetAsync(
        Guid tenantId,
        string templateId,
        NotificationChannel channel,
        CancellationToken cancellationToken);
}
