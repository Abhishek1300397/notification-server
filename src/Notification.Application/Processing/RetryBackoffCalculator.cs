using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Processing;
using Notification.Application.Configuration;

namespace Notification.Application.Processing;

public sealed class RetryBackoffCalculator : IRetryBackoffCalculator
{
    private readonly NotificationRetryOptions _options;

    public RetryBackoffCalculator(IOptions<NotificationRetryOptions> options)
    {
        _options = options.Value;
    }

    public TimeSpan GetDelay(int failedAttemptNumber)
    {
        if (failedAttemptNumber <= 0)
        {
            return TimeSpan.Zero;
        }

        if (_options.DelaySeconds is { Length: > 0 })
        {
            var index = Math.Min(failedAttemptNumber - 1, _options.DelaySeconds.Length - 1);
            var configured = Math.Clamp(_options.DelaySeconds[index], 0, _options.MaxDelaySeconds);
            return TimeSpan.FromSeconds(configured);
        }

        var exponential = _options.InitialDelaySeconds * Math.Pow(2, failedAttemptNumber - 1);
        var seconds = Math.Min(exponential, _options.MaxDelaySeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    public DateTime GetNextAttemptUtc(int failedAttemptNumber, DateTime utcNow) =>
        utcNow.Add(GetDelay(failedAttemptNumber));
}
