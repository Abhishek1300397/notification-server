using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Messaging;
using Notification.Application.Configuration;
using RabbitMQ.Client;

namespace Notification.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqMessagePublisher : IMessagePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRabbitMqConnectionManager _connections;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqMessagePublisher> _logger;

    public RabbitMqMessagePublisher(
        IRabbitMqConnectionManager connections,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqMessagePublisher> logger)
    {
        _connections = connections;
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishAsync<T>(
        string exchange,
        string routingKey,
        T message,
        IReadOnlyDictionary<string, object>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        return PublishJsonAsync(exchange, routingKey, json, headers, 5, cancellationToken);
    }

    public Task PublishJsonAsync(
        string exchange,
        string routingKey,
        string jsonPayload,
        IReadOnlyDictionary<string, object>? headers,
        CancellationToken cancellationToken) =>
        PublishJsonAsync(exchange, routingKey, jsonPayload, headers, 5, cancellationToken);

    public async Task PublishJsonAsync(
        string exchange,
        string routingKey,
        string jsonPayload,
        IReadOnlyDictionary<string, object>? headers,
        byte priority,
        CancellationToken cancellationToken)
    {
        var connection = await _connections.GetConnectionAsync(cancellationToken);
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using var channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString("N"),
            Priority = Math.Min(priority, _options.MaxPriority),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = headers?.ToDictionary(k => k.Key, v => (object?)v.Value)
        };

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(jsonPayload),
            cancellationToken: cancellationToken);

        _logger.LogDebug(
            "Published message to {Exchange}/{RoutingKey} with priority {Priority}",
            exchange,
            routingKey,
            priority);
    }
}
