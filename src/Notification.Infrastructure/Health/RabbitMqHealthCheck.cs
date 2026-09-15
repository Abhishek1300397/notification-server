using Microsoft.Extensions.Diagnostics.HealthChecks;
using Notification.Infrastructure.Messaging.RabbitMq;
using Notification.Infrastructure.Observability;

namespace Notification.Infrastructure.Health;

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IRabbitMqConnectionManager _connections;

    public RabbitMqHealthCheck(IRabbitMqConnectionManager connections)
    {
        _connections = connections;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_connections.IsConnected
            ? HealthCheckResult.Healthy("RabbitMQ connection is open.")
            : HealthCheckResult.Unhealthy("RabbitMQ connection is not open."));
    }
}

public sealed class WorkerHealthCheck : IHealthCheck
{
    private readonly WorkerHealthState _state;

    public WorkerHealthCheck(WorkerHealthState state)
    {
        _state = state;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_state.OutboxLoopRunning && _state.ConsumerRunning)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Background workers are running."));
        }

        return Task.FromResult(HealthCheckResult.Degraded("One or more background workers are not running."));
    }
}
