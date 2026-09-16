using System.Text.Json.Serialization;

namespace Notification.Application.Messaging;

public sealed class NotificationMessage
{
    public Guid NotificationId { get; init; }
    public Guid TenantId { get; init; }
    public string Channel { get; init; } = default!;
    public string Priority { get; init; } = nameof(Domain.Enums.NotificationPriority.Normal);
    public string TemplateId { get; init; } = default!;
    public IReadOnlyDictionary<string, string>? Data { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    [JsonIgnore]
    public string MessageType => "notification.created";
}
