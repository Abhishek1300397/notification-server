using Notification.Application.Abstractions.Time;

namespace Notification.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
