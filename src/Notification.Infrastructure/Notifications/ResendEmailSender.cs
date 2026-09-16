using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Notifications;
using Notification.Application.Configuration;
using Notification.Domain.Exceptions;

namespace Notification.Infrastructure.Notifications;

public sealed class ResendEmailSender : IEmailSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(
        HttpClient httpClient,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailSendRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.From))
        {
            throw new PermanentNotificationException("Resend is not configured. Set Resend:ApiKey and Resend:From.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(BuildPayload(request), options: JsonOptions)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        message.Headers.TryAddWithoutValidation("Idempotency-Key", $"notification/{request.NotificationId:N}");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new TransientNotificationException("Resend request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new TransientNotificationException("Resend is temporarily unavailable.", ex);
        }

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
        {
            _logger.LogInformation("Sent email via Resend for notification {NotificationId}", request.NotificationId);
            return;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogInformation(
                "Resend reported an idempotent replay for notification {NotificationId}",
                request.NotificationId);
            return;
        }

        var error = await ReadErrorAsync(response, cancellationToken);
        if (IsTransient(response.StatusCode))
        {
            throw new TransientNotificationException($"Resend returned {(int)response.StatusCode}: {error}");
        }

        throw new PermanentNotificationException($"Resend rejected the email ({(int)response.StatusCode}): {error}");
    }

    private object BuildPayload(EmailSendRequest request)
    {
        var html = LooksLikeHtml(request.Body)
            ? request.Body
            : $"<p>{WebUtility.HtmlEncode(request.Body)}</p>";

        return new
        {
            from = _options.From,
            to = new[] { request.To },
            subject = request.Subject,
            html,
            reply_to = string.IsNullOrWhiteSpace(_options.ReplyTo) ? null : _options.ReplyTo,
            tags = new[]
            {
                new { name = "priority", value = request.Priority.ToString() },
                new { name = "notification_id", value = request.NotificationId.ToString("N") }
            }
        };
    }

    private static bool LooksLikeHtml(string body) =>
        body.Contains('<', StringComparison.Ordinal) && body.Contains('>', StringComparison.Ordinal);

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            || (int)statusCode >= 500;

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ResendErrorResponse>(JsonOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(payload?.Message))
            {
                return payload.Message.Length <= 256 ? payload.Message : payload.Message[..256];
            }
        }
        catch (JsonException)
        {
        }

        return response.ReasonPhrase ?? "unknown error";
    }

    private sealed class ResendErrorResponse
    {
        public string? Name { get; set; }
        public string? Message { get; set; }
    }
}
