using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notification.Application;
using Notification.Application.Abstractions.Messaging;
using Notification.Application.Abstractions.Notifications;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Persistence;
using Notification.Application.Abstractions.Processing;
using Notification.Application.Abstractions.Time;
using Notification.Application.Configuration;
using Notification.Infrastructure.BackgroundJobs;
using Notification.Infrastructure.Health;
using Notification.Infrastructure.Messaging;
using Notification.Infrastructure.Messaging.RabbitMq;
using Notification.Infrastructure.Notifications.Channels;
using Notification.Infrastructure.Observability;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Persistence.Repositories;
using Notification.Infrastructure.Time;

namespace Notification.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddNotificationApplication();
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<NotificationRetryOptions>(configuration.GetSection(NotificationRetryOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Notifications")
            ?? throw new InvalidOperationException("Connection string 'Notifications' is not configured.");

        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IOutboxPublisher, OutboxPublisher>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<INotificationMetrics, NotificationMetrics>();
        services.AddSingleton<WorkerHealthState>();
        services.AddSingleton<IRabbitMqConnectionManager, RabbitMqConnectionManager>();
        services.AddSingleton<RabbitMqTopology>();
        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();

        services.AddSingleton<INotificationChannel, EmailNotificationChannel>();
        services.AddSingleton<INotificationChannel, SmsNotificationChannel>();
        services.AddSingleton<INotificationChannel, PushNotificationChannel>();

        services.AddHostedService<OutboxBackgroundService>();
        services.AddHostedService<NotificationConsumerHostedService>();

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql")
            .AddDbContextCheck<NotificationDbContext>("efcore")
            .AddCheck<RabbitMqHealthCheck>("rabbitmq")
            .AddCheck<WorkerHealthCheck>("workers");

        return services;
    }

    public static async Task InitializeNotificationDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await NotificationDbSeeder.SeedAsync(db, cancellationToken);
    }
}
