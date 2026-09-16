using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Application.Abstractions.Notifications;

public interface INotificationChannel
{
    NotificationChannel Channel { get; }

    Task SendAsync(
        NotificationRequest notification,
        RenderedTemplate renderedTemplate,
        CancellationToken cancellationToken);
}

public sealed record RenderedTemplate(string Subject, string Body);
