using Notification.Application.Notifications;

namespace Notification.Application.Abstractions.Notifications;

public interface INotificationApplicationService
{
    Task<CreateNotificationResponse> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken);

    Task<NotificationStatusResponse?> GetAsync(Guid notificationId, CancellationToken cancellationToken);
}
