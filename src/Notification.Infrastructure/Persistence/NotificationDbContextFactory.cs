using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Persistence;

public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Notifications")
            ?? "Host=localhost;Port=5432;Database=notifications;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new NotificationDbContext(options);
    }
}
