using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Notifications;
using Notification.Application.Configuration;
using Notification.Domain.Enums;
using Notification.Domain.Exceptions;
using Notification.Infrastructure.Notifications;

namespace Notification.UnitTests;

public sealed class ResendEmailSenderTests
{
    [Fact]
    public async Task Sends_email_with_priority_tag_and_idempotency_key()
    {
        string? body = null;
        string? idempotency = null;
        string? auth = null;
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"email_1"}""", async request =>
        {
            auth = request.Headers.Authorization?.ToString();
            idempotency = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
        });
        var sender = CreateSender(handler);

        await sender.SendAsync(new EmailSendRequest(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "user@example.com",
            "Hello",
            "Welcome Ada",
            NotificationPriority.High), CancellationToken.None);

        Assert.Equal("Bearer test-key", auth);
        Assert.Equal("notification/aaaaaaaabbbbccccddddeeeeeeeeeeee", idempotency);
        Assert.Contains("\"name\":\"priority\"", body);
        Assert.Contains("\"value\":\"High\"", body);
        Assert.Contains("user@example.com", body);
    }

    [Fact]
    public async Task Treats_429_as_transient()
    {
        var sender = CreateSender(new StubHandler(HttpStatusCode.TooManyRequests, """{"message":"rate limited"}"""));

        await Assert.ThrowsAsync<TransientNotificationException>(() =>
            sender.SendAsync(CreateRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Treats_422_as_permanent()
    {
        var sender = CreateSender(new StubHandler(HttpStatusCode.UnprocessableEntity, """{"message":"invalid from"}"""));

        await Assert.ThrowsAsync<PermanentNotificationException>(() =>
            sender.SendAsync(CreateRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Missing_configuration_is_permanent()
    {
        var sender = CreateSender(
            new StubHandler(HttpStatusCode.OK, "{}"),
            new ResendOptions { ApiKey = "", From = "" });

        await Assert.ThrowsAsync<PermanentNotificationException>(() =>
            sender.SendAsync(CreateRequest(), CancellationToken.None));
    }

    private static EmailSendRequest CreateRequest() =>
        new(Guid.NewGuid(), "user@example.com", "Hi", "Body", NotificationPriority.Normal);

    private static ResendEmailSender CreateSender(HttpMessageHandler handler, ResendOptions? options = null)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        return new ResendEmailSender(
            client,
            Options.Create(options ?? new ResendOptions
            {
                ApiKey = "test-key",
                From = "Notify <noreply@example.com>"
            }),
            NullLogger<ResendEmailSender>.Instance);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly Func<HttpRequestMessage, Task>? _capture;

        public StubHandler(HttpStatusCode status, string body, Func<HttpRequestMessage, Task>? capture = null)
        {
            _status = status;
            _body = body;
            _capture = capture;
        }

        public StubHandler(HttpStatusCode status, string body, Action<HttpRequestMessage> capture)
            : this(status, body, request =>
            {
                capture(request);
                return Task.CompletedTask;
            })
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_capture is not null)
            {
                await _capture(request);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
