using Notification.Domain.Enums;

namespace Notification.Application.Abstractions.Notifications;

public sealed record EmailSendRequest(
    Guid NotificationId,
    string To,
    string Subject,
    string Body,
    NotificationPriority Priority);

public interface IEmailSender
{
    Task SendAsync(EmailSendRequest request, CancellationToken cancellationToken);
}
