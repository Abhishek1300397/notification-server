using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    public DbSet<NotificationRequest> Notifications => Set<NotificationRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
    }
}
