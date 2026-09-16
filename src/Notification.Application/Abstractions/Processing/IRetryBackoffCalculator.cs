namespace Notification.Application.Abstractions.Processing;

public interface IRetryBackoffCalculator
{
    TimeSpan GetDelay(int failedAttemptNumber);

    DateTime GetNextAttemptUtc(int failedAttemptNumber, DateTime utcNow);
}
