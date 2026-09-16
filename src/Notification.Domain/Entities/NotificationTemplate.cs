using Notification.Domain.Enums;

namespace Notification.Domain.Entities;

public sealed class NotificationTemplate
{
    public string Id { get; set; } = default!;
    public Guid TenantId { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Subject { get; set; } = default!;
    public string Body { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
