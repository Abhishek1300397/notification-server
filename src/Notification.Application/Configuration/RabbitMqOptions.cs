namespace Notification.Application.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string Exchange { get; set; } = "notifications";
    public string Queue { get; set; } = "notification.processing";
    public string RoutingKey { get; set; } = "notification.created";
    public string DeadLetterExchange { get; set; } = "notifications.dlx";
    public string DeadLetterQueue { get; set; } = "notification.dlq";
    public string DeadLetterRoutingKey { get; set; } = "notification.failed";
    public string RetryExchange { get; set; } = "notifications.retry";
    public ushort PrefetchCount { get; set; } = 20;

    /// <summary>
    /// Competing consumers in this process. Increase with PrefetchCount, or run more instances.
    /// </summary>
    public int ConsumerCount { get; set; } = 1;

    /// <summary>
    /// RabbitMQ x-max-priority. Messages use 1 (Low) through 9 (Critical).
    /// Changing this on an existing queue requires deleting and recreating that queue.
    /// </summary>
    public byte MaxPriority { get; set; } = 9;
}
