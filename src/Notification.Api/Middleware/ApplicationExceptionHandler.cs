using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Notification.Api.Middleware;

public sealed class ApplicationExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApplicationExceptionHandler> _logger;

    public ApplicationExceptionHandler(ILogger<ApplicationExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ArgumentException argumentException)
        {
            return false;
        }

        _logger.LogWarning(argumentException, "Rejected notification request");

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid notification request",
            Detail = argumentException.Message
        };

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
