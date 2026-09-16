using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions.Notifications;
using Notification.Application.Abstractions.Persistence;
using Notification.Application.Abstractions.Time;
using Notification.Application.Messaging;
using Notification.Application.Notifications;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Application.Notifications;

public sealed class NotificationApplicationService : INotificationApplicationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly INotificationRepository _notifications;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<NotificationApplicationService> _logger;

    public NotificationApplicationService(
        INotificationRepository notifications,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<NotificationApplicationService> logger)
    {
        _notifications = notifications;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CreateNotificationResponse> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _notifications.GetByIdempotencyKeyAsync(
                request.TenantId,
                request.IdempotencyKey,
                cancellationToken);

            if (existing is not null)
            {
                _logger.LogInformation(
                    "Returning existing notification {NotificationId} for tenant {TenantId} due to idempotency key",
                    existing.Id,
                    existing.TenantId);

                return new CreateNotificationResponse(existing.Id, existing.TenantId, existing.Status.ToString());
            }
        }

        var now = _clock.UtcNow;
        var notification = new NotificationRequest
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Channel = request.Channel,
            Recipient = request.Recipient.Trim(),
            TemplateId = request.TemplateId.Trim(),
            DataJson = JsonSerializer.Serialize(request.Data ?? new Dictionary<string, string>(), JsonOptions),
            Status = NotificationStatus.Pending,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
            CreatedAtUtc = now
        };

        var message = new NotificationMessage
        {
            NotificationId = notification.Id,
            TenantId = notification.TenantId,
            Channel = notification.Channel.ToString(),
            TemplateId = notification.TemplateId,
            Data = request.Data,
            CreatedAtUtc = now
        };

        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = notification.TenantId,
            NotificationId = notification.Id,
            Type = message.MessageType,
            Payload = JsonSerializer.Serialize(message, JsonOptions),
            Status = OutboxStatus.Pending,
            CreatedAtUtc = now,
            NextAttemptAtUtc = now
        };

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _notifications.AddAsync(notification, ct);
            await _outbox.AddAsync(outbox, ct);
            await _notifications.SaveChangesAsync(ct);
        }, cancellationToken);

        _logger.LogInformation(
            "Created notification {NotificationId} for tenant {TenantId} on channel {Channel}",
            notification.Id,
            notification.TenantId,
            notification.Channel);

        return new CreateNotificationResponse(notification.Id, notification.TenantId, notification.Status.ToString());
    }

    public async Task<NotificationStatusResponse?> GetAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await _notifications.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null)
        {
            return null;
        }

        return new NotificationStatusResponse(
            notification.Id,
            notification.TenantId,
            notification.Channel.ToString(),
            notification.Status.ToString(),
            notification.AttemptCount,
            notification.CreatedAtUtc,
            notification.SentAtUtc,
            notification.FailedAtUtc);
    }

    private static void Validate(CreateNotificationRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Recipient))
        {
            throw new ArgumentException("Recipient is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            throw new ArgumentException("TemplateId is required.", nameof(request));
        }

        if (!Enum.IsDefined(request.Channel))
        {
            throw new ArgumentException("Channel is not supported.", nameof(request));
        }
    }
}
