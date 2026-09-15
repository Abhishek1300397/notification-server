using Microsoft.AspNetCore.Mvc;
using Notification.Application.Abstractions.Notifications;
using Notification.Application.Notifications;

namespace Notification.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationApplicationService _notifications;

    public NotificationsController(INotificationApplicationService notifications)
    {
        _notifications = notifications;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateNotificationResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _notifications.CreateAsync(request, cancellationToken);
        return AcceptedAtAction(nameof(GetById), new { notificationId = result.NotificationId }, result);
    }

    [HttpGet("{notificationId:guid}")]
    [ProducesResponseType(typeof(NotificationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid notificationId, CancellationToken cancellationToken)
    {
        var result = await _notifications.GetAsync(notificationId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
