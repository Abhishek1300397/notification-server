using System.ComponentModel.DataAnnotations;
using Notification.Domain.Enums;

namespace Notification.Application.Notifications;

public sealed class CreateNotificationRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [EnumDataType(typeof(NotificationChannel))]
    public NotificationChannel Channel { get; set; }

    [EnumDataType(typeof(NotificationPriority))]
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    [Required]
    [MaxLength(320)]
    public string Recipient { get; set; } = default!;

    [Required]
    [MaxLength(128)]
    public string TemplateId { get; set; } = default!;

    public Dictionary<string, string>? Data { get; set; }

    [MaxLength(128)]
    public string? IdempotencyKey { get; set; }
}

public sealed record CreateNotificationResponse(
    Guid NotificationId,
    Guid TenantId,
    string Status,
    string Priority);

public sealed record NotificationStatusResponse(
    Guid NotificationId,
    Guid TenantId,
    string Channel,
    string Status,
    string Priority,
    int AttemptCount,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc,
    DateTime? FailedAtUtc);
