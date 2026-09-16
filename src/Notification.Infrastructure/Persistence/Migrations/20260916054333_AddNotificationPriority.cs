using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_status_next_attempt",
                table: "outbox_messages");

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "notifications",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_next_attempt",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at_utc", "priority", "created_at_utc" },
                descending: new[] { false, false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_status_next_attempt",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_next_attempt",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at_utc", "created_at_utc" });
        }
    }
}
