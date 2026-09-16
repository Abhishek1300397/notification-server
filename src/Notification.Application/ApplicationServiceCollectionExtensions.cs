using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Abstractions.Messaging;
using Notification.Application.Abstractions.Notifications;
using Notification.Application.Abstractions.Processing;
using Notification.Application.Messaging;
using Notification.Application.Notifications;
using Notification.Application.Processing;

namespace Notification.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        services.AddScoped<INotificationApplicationService, NotificationApplicationService>();
        services.AddScoped<INotificationProcessor, NotificationProcessor>();
        services.AddSingleton<INotificationChannelResolver, NotificationChannelResolver>();
        services.AddSingleton<ITemplateRenderer, SimpleTemplateRenderer>();
        services.AddSingleton<INotificationMessageValidator, NotificationMessageValidator>();
        services.AddSingleton<IRetryBackoffCalculator, RetryBackoffCalculator>();
        return services;
    }
}
