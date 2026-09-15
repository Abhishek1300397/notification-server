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
}
