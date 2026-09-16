using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Notification.Application.Abstractions.Messaging;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Persistence;
using Notification.Application.Abstractions.Processing;
using Notification.Application.Abstractions.Time;
using Notification.Application.Configuration;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Messaging;

namespace Notification.UnitTests;

public sealed class OutboxPublisherTests
{
    [Fact]
    public async Task Publishes_claimed_messages_and_marks_them_published()
    {
        var message = CreateOutbox();
        var outbox = Substitute.For<IOutboxRepository>();
        outbox.CountPendingAsync(Arg.Any<CancellationToken>()).Returns(1);
        outbox.ClaimPendingAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns([message]);

        var publisher = Substitute.For<IMessagePublisher>();
        var sut = CreateSut(outbox, publisher);

        await sut.ProcessBatchAsync(CancellationToken.None);

        await publisher.Received(1).PublishJsonAsync(
            "notifications",
            "notification.created",
            message.Payload,
            Arg.Any<IReadOnlyDictionary<string, object>?>(),
            Arg.Any<byte>(),
            Arg.Any<CancellationToken>());
        Assert.Equal(OutboxStatus.Published, message.Status);
        await outbox.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leaves_message_pending_when_rabbitmq_is_unavailable()
    {
        var message = CreateOutbox();
        var outbox = Substitute.For<IOutboxRepository>();
        outbox.ClaimPendingAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns([message]);

        var publisher = Substitute.For<IMessagePublisher>();
        publisher.PublishJsonAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, object>?>(),
                Arg.Any<byte>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("broker down"));

        var backoff = Substitute.For<IRetryBackoffCalculator>();
        backoff.GetNextAttemptUtc(Arg.Any<int>(), Arg.Any<DateTime>())
            .Returns(DateTime.UtcNow.AddSeconds(5));

        var sut = CreateSut(outbox, publisher, backoff, configureBackoff: false);

        await sut.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(1, message.RetryCount);
        Assert.True(message.NextAttemptAtUtc > message.CreatedAtUtc);
    }

    private static OutboxPublisher CreateSut(
        IOutboxRepository outbox,
        IMessagePublisher publisher,
        IRetryBackoffCalculator? backoff = null,
        bool configureBackoff = true)
    {
        backoff ??= Substitute.For<IRetryBackoffCalculator>();
        if (configureBackoff)
        {
            backoff.GetNextAttemptUtc(Arg.Any<int>(), Arg.Any<DateTime>()).Returns(ci => (DateTime)ci[1]!);
        }

        return new OutboxPublisher(
            outbox,
            publisher,
            backoff,
            Substitute.For<IClock>(),
            Substitute.For<INotificationMetrics>(),
            Options.Create(new OutboxOptions { BatchSize = 10, MaxRetryCount = 5, LockDurationSeconds = 30 }),
            Options.Create(new RabbitMqOptions()),
            NullLogger<OutboxPublisher>.Instance);
    }

    private static OutboxMessage CreateOutbox() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        NotificationId = Guid.NewGuid(),
        Type = "notification.created",
        Payload = """{"notificationId":"11111111-1111-1111-1111-111111111111"}""",
        Status = OutboxStatus.Processing,
        CreatedAtUtc = DateTime.UtcNow,
        NextAttemptAtUtc = DateTime.UtcNow
    };
}
