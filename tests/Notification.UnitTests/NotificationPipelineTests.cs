using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Notification.Application.Abstractions.Notifications;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Persistence;
using Notification.Application.Abstractions.Time;
using Notification.Application.Configuration;
using Notification.Application.Messaging;
using Notification.Application.Notifications;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Domain.Exceptions;

namespace Notification.UnitTests;

public sealed class RetryBackoffCalculatorTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 30)]
    [InlineData(3, 120)]
    [InlineData(4, 600)]
    [InlineData(5, 600)]
    public void Uses_configured_delay_ladder(int attempt, int expectedSeconds)
    {
        var calculator = new Notification.Application.Processing.RetryBackoffCalculator(
            Options.Create(new NotificationRetryOptions
            {
                DelaySeconds = [5, 30, 120, 600],
                MaxDelaySeconds = 600
            }));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), calculator.GetDelay(attempt));
    }

    [Fact]
    public void Falls_back_to_exponential_backoff()
    {
        var calculator = new Notification.Application.Processing.RetryBackoffCalculator(
            Options.Create(new NotificationRetryOptions
            {
                DelaySeconds = [],
                InitialDelaySeconds = 5,
                MaxDelaySeconds = 20
            }));

        Assert.Equal(TimeSpan.FromSeconds(5), calculator.GetDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(10), calculator.GetDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(20), calculator.GetDelay(3));
    }
}

public sealed class NotificationMessageValidatorTests
{
    private readonly NotificationMessageValidator _validator = new();

    [Fact]
    public void Accepts_valid_message()
    {
        _validator.Validate(new NotificationMessage
        {
            NotificationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Channel = "Email",
            TemplateId = "welcome",
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    [Fact]
    public void Rejects_missing_ids_as_permanent()
    {
        Assert.Throws<PermanentNotificationException>(() => _validator.Validate(new NotificationMessage
        {
            Channel = "Email",
            TemplateId = "welcome"
        }));
    }
}

public sealed class SimpleTemplateRendererTests
{
    [Fact]
    public void Replaces_placeholders()
    {
        var renderer = new SimpleTemplateRenderer();
        var template = new NotificationTemplate
        {
            Id = "welcome",
            Subject = "Hi {{name}}",
            Body = "Welcome {{name}} to {{product}}"
        };

        var rendered = renderer.Render(template, new Dictionary<string, string>
        {
            ["name"] = "Ada",
            ["product"] = "Notify"
        });

        Assert.Equal("Hi Ada", rendered.Subject);
        Assert.Equal("Welcome Ada to Notify", rendered.Body);
    }
}

public sealed class NotificationProcessorTests
{
    [Fact]
    public async Task Does_not_send_when_already_sent()
    {
        var notification = CreateNotification(NotificationStatus.Sent);
        var channel = Substitute.For<INotificationChannel>();
        var processor = CreateProcessor(notification, channel);

        await processor.ProcessAsync(CreateMessage(notification), CancellationToken.None);

        await channel.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task Sends_and_marks_sent()
    {
        var notification = CreateNotification(NotificationStatus.Pending);
        var channel = Substitute.For<INotificationChannel>();
        channel.Channel.Returns(NotificationChannel.Email);
        var processor = CreateProcessor(notification, channel);

        await processor.ProcessAsync(CreateMessage(notification), CancellationToken.None);

        await channel.Received(1).SendAsync(notification, Arg.Any<RenderedTemplate>(), Arg.Any<CancellationToken>());
        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }

    [Fact]
    public async Task Transient_failure_marks_retrying()
    {
        var notification = CreateNotification(NotificationStatus.Pending);
        var channel = Substitute.For<INotificationChannel>();
        channel.Channel.Returns(NotificationChannel.Email);
        channel.SendAsync(Arg.Any<NotificationRequest>(), Arg.Any<RenderedTemplate>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new TransientNotificationException("smtp timeout")));

        var processor = CreateProcessor(notification, channel);

        await Assert.ThrowsAsync<TransientNotificationException>(() =>
            processor.ProcessAsync(CreateMessage(notification), CancellationToken.None));

        Assert.Equal(NotificationStatus.Retrying, notification.Status);
        Assert.Equal(1, notification.AttemptCount);
    }

    [Fact]
    public async Task Permanent_failure_marks_failed()
    {
        var notification = CreateNotification(NotificationStatus.Pending);
        var channel = Substitute.For<INotificationChannel>();
        channel.Channel.Returns(NotificationChannel.Email);
        channel.SendAsync(Arg.Any<NotificationRequest>(), Arg.Any<RenderedTemplate>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new PermanentNotificationException("invalid recipient")));

        var processor = CreateProcessor(notification, channel);

        await Assert.ThrowsAsync<PermanentNotificationException>(() =>
            processor.ProcessAsync(CreateMessage(notification), CancellationToken.None));

        Assert.Equal(NotificationStatus.Failed, notification.Status);
    }

    [Fact]
    public async Task Exhausted_retries_become_permanent()
    {
        var notification = CreateNotification(NotificationStatus.Pending);
        notification.AttemptCount = 4;
        var channel = Substitute.For<INotificationChannel>();
        channel.Channel.Returns(NotificationChannel.Email);
        channel.SendAsync(Arg.Any<NotificationRequest>(), Arg.Any<RenderedTemplate>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new TransientNotificationException("timeout")));

        var processor = CreateProcessor(notification, channel, maxAttempts: 5);

        await Assert.ThrowsAsync<PermanentNotificationException>(() =>
            processor.ProcessAsync(CreateMessage(notification), CancellationToken.None));

        Assert.Equal(NotificationStatus.Failed, notification.Status);
    }

    [Fact]
    public async Task Missing_template_is_permanent()
    {
        var notification = CreateNotification(NotificationStatus.Pending);
        var templates = Substitute.For<ITemplateRepository>();
        templates.GetAsync(default, default!, default, default)
            .ReturnsForAnyArgs((NotificationTemplate?)null);

        var processor = CreateProcessor(notification, Substitute.For<INotificationChannel>(), templates: templates);

        await Assert.ThrowsAsync<PermanentNotificationException>(() =>
            processor.ProcessAsync(CreateMessage(notification), CancellationToken.None));

        Assert.Equal(NotificationStatus.Failed, notification.Status);
    }

    private static NotificationProcessor CreateProcessor(
        NotificationRequest notification,
        INotificationChannel channel,
        ITemplateRepository? templates = null,
        int maxAttempts = 5)
    {
        var notifications = new InMemoryNotificationRepository(notification);
        templates ??= CreateTemplates(notification);
        var renderer = new SimpleTemplateRenderer();
        var resolver = new NotificationChannelResolver([channel]);
        channel.Channel.Returns(notification.Channel);

        return new NotificationProcessor(
            notifications,
            templates,
            renderer,
            resolver,
            new FixedClock(DateTime.UtcNow),
            Substitute.For<INotificationMetrics>(),
            Options.Create(new NotificationRetryOptions { MaxAttempts = maxAttempts }),
            NullLogger<NotificationProcessor>.Instance);
    }

    private static ITemplateRepository CreateTemplates(NotificationRequest notification)
    {
        var templates = Substitute.For<ITemplateRepository>();
        templates.GetAsync(notification.TenantId, notification.TemplateId, notification.Channel, Arg.Any<CancellationToken>())
            .Returns(new NotificationTemplate
            {
                Id = notification.TemplateId,
                TenantId = notification.TenantId,
                Channel = notification.Channel,
                Subject = "Hi {{name}}",
                Body = "Hello {{name}}",
                IsActive = true
            });
        return templates;
    }

    private static NotificationRequest CreateNotification(NotificationStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Channel = NotificationChannel.Email,
        Recipient = "user@example.com",
        TemplateId = "welcome",
        DataJson = """{"name":"Ada"}""",
        Status = status,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static NotificationMessage CreateMessage(NotificationRequest notification) => new()
    {
        NotificationId = notification.Id,
        TenantId = notification.TenantId,
        Channel = notification.Channel.ToString(),
        TemplateId = notification.TemplateId,
        CreatedAtUtc = notification.CreatedAtUtc
    };
}

public sealed class NotificationApplicationServiceTests
{
    [Fact]
    public async Task Creates_notification_and_outbox_in_one_transaction()
    {
        var notifications = new InMemoryNotificationRepository();
        var outbox = new InMemoryOutboxRepository();
        var uow = new FakeUnitOfWork();
        var service = new NotificationApplicationService(
            notifications,
            outbox,
            uow,
            new FixedClock(DateTime.UtcNow),
            NullLogger<NotificationApplicationService>.Instance);

        var result = await service.CreateAsync(new CreateNotificationRequest
        {
            TenantId = Guid.NewGuid(),
            Channel = NotificationChannel.Email,
            Recipient = "user@example.com",
            TemplateId = "welcome",
            Data = new Dictionary<string, string> { ["name"] = "Ada" }
        }, CancellationToken.None);

        Assert.Equal("Pending", result.Status);
        Assert.Single(notifications.Items);
        Assert.Single(outbox.Items);
        Assert.True(uow.Executed);
    }

    [Fact]
    public async Task Reuses_existing_notification_for_idempotency_key()
    {
        var tenantId = Guid.NewGuid();
        var existing = new NotificationRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Channel = NotificationChannel.Email,
            Recipient = "user@example.com",
            TemplateId = "welcome",
            Status = NotificationStatus.Pending,
            IdempotencyKey = "key-1",
            CreatedAtUtc = DateTime.UtcNow
        };
        var notifications = new InMemoryNotificationRepository(existing);
        var service = new NotificationApplicationService(
            notifications,
            new InMemoryOutboxRepository(),
            new FakeUnitOfWork(),
            new FixedClock(DateTime.UtcNow),
            NullLogger<NotificationApplicationService>.Instance);

        var result = await service.CreateAsync(new CreateNotificationRequest
        {
            TenantId = tenantId,
            Channel = NotificationChannel.Email,
            Recipient = "user@example.com",
            TemplateId = "welcome",
            IdempotencyKey = "key-1"
        }, CancellationToken.None);

        Assert.Equal(existing.Id, result.NotificationId);
        Assert.Single(notifications.Items);
    }
}

file sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = utcNow;
}

file sealed class FakeUnitOfWork : IUnitOfWork
{
    public bool Executed { get; private set; }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        Executed = true;
        await action(cancellationToken);
    }
}

file sealed class InMemoryNotificationRepository : INotificationRepository
{
    public List<NotificationRequest> Items { get; } = [];

    public InMemoryNotificationRepository(params NotificationRequest[] items)
    {
        Items.AddRange(items);
    }

    public Task AddAsync(NotificationRequest notification, CancellationToken cancellationToken)
    {
        Items.Add(notification);
        return Task.CompletedTask;
    }

    public Task<NotificationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<NotificationRequest?> GetByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.TenantId == tenantId && x.IdempotencyKey == idempotencyKey));

    public Task<bool> TryClaimForProcessingAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = Items.First(x => x.Id == id);
        if (!item.CanStartProcessing)
        {
            return Task.FromResult(false);
        }

        item.MarkProcessing(DateTime.UtcNow);
        return Task.FromResult(true);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

file sealed class InMemoryOutboxRepository : IOutboxRepository
{
    public List<OutboxMessage> Items { get; } = [];

    public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        Items.Add(message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize,
        TimeSpan lockDuration,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OutboxMessage>>(Items.Take(batchSize).ToList());

    public Task<int> CountPendingAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(x => x.Status == OutboxStatus.Pending));

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
