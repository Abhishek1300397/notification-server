namespace Notification.Application.Abstractions.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<T>(
        string exchange,
        string routingKey,
        T message,
        IReadOnlyDictionary<string, object>? headers = null,
        CancellationToken cancellationToken = default);

    Task PublishJsonAsync(
        string exchange,
        string routingKey,
        string jsonPayload,
        IReadOnlyDictionary<string, object>? headers = null,
        CancellationToken cancellationToken = default) =>
        PublishJsonAsync(exchange, routingKey, jsonPayload, headers, priority: 5, cancellationToken);

    Task PublishJsonAsync(
        string exchange,
        string routingKey,
        string jsonPayload,
        IReadOnlyDictionary<string, object>? headers,
        byte priority,
        CancellationToken cancellationToken);
}
