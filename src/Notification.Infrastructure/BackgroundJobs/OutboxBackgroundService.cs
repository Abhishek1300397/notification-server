using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Processing;
using Notification.Application.Configuration;
using Notification.Infrastructure.Observability;

namespace Notification.Infrastructure.BackgroundJobs;

public sealed class OutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly WorkerHealthState _health;
    private readonly ILogger<OutboxBackgroundService> _logger;

    public OutboxBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        WorkerHealthState health,
        ILogger<OutboxBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _health = health;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _health.SetOutboxRunning(true);
        _logger.LogInformation(
            "Outbox worker started. Workers={WorkerCount} BatchSize={BatchSize} PollingIntervalSeconds={PollingIntervalSeconds}",
            Math.Max(1, _options.WorkerCount),
            _options.BatchSize,
            _options.PollingIntervalSeconds);

        try
        {
            var workerCount = Math.Max(1, _options.WorkerCount);
            var workers = Enumerable.Range(0, workerCount)
                .Select(index => RunLoopAsync(index, stoppingToken));
            await Task.WhenAll(workers);
        }
        finally
        {
            _health.SetOutboxRunning(false);
            _logger.LogInformation("Outbox worker stopped");
        }
    }

    private async Task RunLoopAsync(int workerIndex, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox poller {WorkerIndex} running", workerIndex);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();
                await publisher.ProcessBatchAsync(stoppingToken);
                _health.MarkOutboxPoll();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox poller {WorkerIndex} iteration failed", workerIndex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
