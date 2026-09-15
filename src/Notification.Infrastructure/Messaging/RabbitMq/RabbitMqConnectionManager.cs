using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Messaging;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Configuration;
using RabbitMQ.Client;

namespace Notification.Infrastructure.Messaging.RabbitMq;

public interface IRabbitMqConnectionManager
{
    bool IsConnected { get; }

    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken);
}

public sealed class RabbitMqConnectionManager : IRabbitMqConnectionManager, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly INotificationMetrics _metrics;
    private readonly ILogger<RabbitMqConnectionManager> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnectionManager(
        IOptions<RabbitMqOptions> options,
        INotificationMetrics metrics,
        ILogger<RabbitMqConnectionManager> logger)
    {
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
    }

    public bool IsConnected => _connection is { IsOpen: true };

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return _connection!;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                return _connection!;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(30),
                ClientProvidedName = "notification-manager"
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _connection.ConnectionShutdownAsync += (_, args) =>
            {
                _metrics.SetRabbitMqConnected(false);
                _logger.LogWarning("RabbitMQ connection shut down. ReplyText={ReplyText}", args.ReplyText);
                return Task.CompletedTask;
            };
            _connection.RecoverySucceededAsync += (_, _) =>
            {
                _metrics.SetRabbitMqConnected(true);
                _logger.LogInformation("RabbitMQ connection recovered");
                return Task.CompletedTask;
            };

            _metrics.SetRabbitMqConnected(true);
            _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", _options.Host, _options.Port);
            return _connection;
        }
        catch
        {
            _metrics.SetRabbitMqConnected(false);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
