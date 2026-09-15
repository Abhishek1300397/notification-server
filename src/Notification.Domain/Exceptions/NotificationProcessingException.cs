using Notification.Domain.Enums;

namespace Notification.Domain.Exceptions;

public abstract class NotificationProcessingException : Exception
{
    protected NotificationProcessingException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public abstract FailureKind Kind { get; }
}

public sealed class TransientNotificationException : NotificationProcessingException
{
    public TransientNotificationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public override FailureKind Kind => FailureKind.Transient;
}

public sealed class PermanentNotificationException : NotificationProcessingException
{
    public PermanentNotificationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public override FailureKind Kind => FailureKind.Permanent;
}
