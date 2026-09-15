namespace Notification.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}
