using Notification.Domain.Enums;
using Notification.Domain.Exceptions;

namespace Notification.IntegrationTests;

public sealed class FailureClassifierTests
{
    [Fact]
    public void Permanent_and_transient_are_distinct()
    {
        Assert.Equal(FailureKind.Permanent, new PermanentNotificationException("bad").Kind);
        Assert.Equal(FailureKind.Transient, new TransientNotificationException("later").Kind);
    }
}
