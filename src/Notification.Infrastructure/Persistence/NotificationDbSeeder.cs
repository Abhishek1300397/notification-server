using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Persistence;

public static class NotificationDbSeeder
{
    public static readonly Guid DemoTenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    public static async Task SeedAsync(NotificationDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.NotificationTemplates.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.NotificationTemplates.AddRange(
            new NotificationTemplate
            {
                Id = "welcome",
                TenantId = DemoTenantId,
                Channel = NotificationChannel.Email,
                Subject = "Welcome {{name}}",
                Body = "Hello {{name}}, welcome to Notification Manager.",
                IsActive = true,
                CreatedAtUtc = now
            },
            new NotificationTemplate
            {
                Id = "otp",
                TenantId = DemoTenantId,
                Channel = NotificationChannel.Sms,
                Subject = "OTP",
                Body = "Your code is {{code}}",
                IsActive = true,
                CreatedAtUtc = now
            },
            new NotificationTemplate
            {
                Id = "alert",
                TenantId = DemoTenantId,
                Channel = NotificationChannel.Push,
                Subject = "{{title}}",
                Body = "{{body}}",
                IsActive = true,
                CreatedAtUtc = now
            });

        await db.SaveChangesAsync(cancellationToken);
    }
}
