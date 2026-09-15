using Notification.Application.Abstractions.Notifications;
using Notification.Domain.Enums;
using Notification.Domain.Exceptions;

namespace Notification.Application.Notifications;

public sealed class NotificationChannelResolver : INotificationChannelResolver
{
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationChannel> _channels;

    public NotificationChannelResolver(IEnumerable<INotificationChannel> channels)
    {
        _channels = channels.ToDictionary(c => c.Channel);
    }

    public INotificationChannel Resolve(NotificationChannel channel)
    {
        if (_channels.TryGetValue(channel, out var implementation))
        {
            return implementation;
        }

        throw new PermanentNotificationException($"Unsupported notification channel {channel}.");
    }
}
