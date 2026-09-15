using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions.Notifications;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Infrastructure.Notifications.Channels;

public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly ILogger<EmailNotificationChannel> _logger;

    public EmailNotificationChannel(ILogger<EmailNotificationChannel> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Email;

    public Task SendAsync(
        NotificationRequest notification,
        RenderedTemplate renderedTemplate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "Fake email channel accepted notification {NotificationId} with subject length {SubjectLength}",
            notification.Id,
            renderedTemplate.Subject.Length);
        return Task.CompletedTask;
    }
}
