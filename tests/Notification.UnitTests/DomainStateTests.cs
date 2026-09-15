using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.UnitTests;

public sealed class NotificationRequestStateTests
{
    [Fact]
    public void Transitions_pending_processing_sent()
    {
        var notification = new NotificationRequest { Status = NotificationStatus.Pending };
        Assert.True(notification.CanStartProcessing);

        notification.MarkProcessing(DateTime.UtcNow);
        Assert.Equal(NotificationStatus.Processing, notification.Status);

        notification.MarkSent(DateTime.UtcNow);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.True(notification.IsTerminalSuccess);
        Assert.False(notification.CanStartProcessing);
    }

    [Fact]
    public void Transient_failure_increments_attempts()
    {
        var notification = new NotificationRequest { Status = NotificationStatus.Processing };
        notification.MarkRetrying(DateTime.UtcNow, "timeout");

        Assert.Equal(NotificationStatus.Retrying, notification.Status);
        Assert.Equal(1, notification.AttemptCount);
        Assert.True(notification.CanStartProcessing);
    }
}

public sealed class NotificationChannelResolverTests
{
    [Fact]
    public void Throws_permanent_for_missing_channel()
    {
        var resolver = new Notification.Application.Notifications.NotificationChannelResolver([]);
        Assert.Throws<Notification.Domain.Exceptions.PermanentNotificationException>(() =>
            resolver.Resolve(NotificationChannel.Email));
    }
}
