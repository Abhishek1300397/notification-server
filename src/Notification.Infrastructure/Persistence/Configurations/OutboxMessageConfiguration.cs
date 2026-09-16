using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Priority).HasConversion<int>();
        builder.Property(x => x.LastError).HasMaxLength(2000);

        // Claim query: WHERE status/next_attempt ready ORDER BY priority DESC, created_at_utc
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.Priority, x.CreatedAtUtc })
            .IsDescending(false, false, true, false)
            .HasDatabaseName("ix_outbox_messages_status_next_attempt");

        builder.HasIndex(x => x.NotificationId)
            .HasDatabaseName("ix_outbox_messages_notification_id");
    }
}
