using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Application.Abstractions.Notifications;

public interface ITemplateRenderer
{
    RenderedTemplate Render(
        NotificationTemplate template,
        IReadOnlyDictionary<string, string> data);
}
