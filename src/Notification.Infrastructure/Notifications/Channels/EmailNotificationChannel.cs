using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions.Notifications;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Infrastructure.Notifications.Channels;

public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailNotificationChannel> _logger;

    public EmailNotificationChannel(
        IEmailSender emailSender,
        ILogger<EmailNotificationChannel> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task SendAsync(
        NotificationRequest notification,
        RenderedTemplate renderedTemplate,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Sending email notification {NotificationId} with priority {Priority}",
            notification.Id,
            notification.Priority);

        await _emailSender.SendAsync(
            new EmailSendRequest(
                notification.Id,
                notification.Recipient,
                renderedTemplate.Subject,
                renderedTemplate.Body,
                notification.Priority),
            cancellationToken);
    }
}
