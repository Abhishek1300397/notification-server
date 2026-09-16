# Notification Manager

Reliable notification API using PostgreSQL outbox processing, RabbitMQ consumers, and [Resend](https://resend.com) for email.

## Run locally

```bash
docker compose up -d
dotnet restore
dotnet ef database update --project src/Notification.Infrastructure --startup-project src/Notification.Api
dotnet run --project src/Notification.Api
```

Create a high-priority email:

```bash
curl -X POST http://localhost:5080/api/notifications \
  -H 'Content-Type: application/json' \
  -d '{
    "tenantId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "channel": "Email",
    "priority": "High",
    "recipient": "user@example.com",
    "templateId": "welcome",
    "data": { "name": "Ada" }
  }'
```

`priority` is `Low`, `Normal` (default), `High`, or `Critical`. Higher priority is claimed first from the outbox and given a higher RabbitMQ priority.

## Email (Resend)

Set these in `appsettings.json` or environment variables. Do not commit a real API key.

```json
"Resend": {
  "ApiKey": "re_xxxxxxxxx",
  "From": "Notification Manager <noreply@your-domain.com>",
  "BaseUrl": "https://api.resend.com",
  "TimeoutSeconds": 30,
  "ReplyTo": ""
}
```

Equivalent environment variables: `Resend__ApiKey`, `Resend__From`.

Each send uses `Idempotency-Key: notification/{id}` and Resend tags `priority` and `notification_id`.

## Increase background throughput

Tune in `appsettings.json` (or env vars):

| Setting | Effect |
|---|---|
| `Outbox:WorkerCount` | Parallel outbox pollers in this process (`FOR UPDATE SKIP LOCKED` keeps them from double-publishing) |
| `Outbox:BatchSize` | Messages claimed per poll |
| `Outbox:PollingIntervalSeconds` | Delay between polls |
| `RabbitMq:ConsumerCount` | Competing consumers in this process |
| `RabbitMq:PrefetchCount` | Unacked messages each consumer can hold |

Also scale out by running more app instances. Each instance runs its own outbox pollers and consumers.

Example:

```json
"Outbox": { "WorkerCount": 4, "BatchSize": 200, "PollingIntervalSeconds": 2 },
"RabbitMq": { "ConsumerCount": 4, "PrefetchCount": 50 }
```

## Tests

```bash
dotnet test
```

## Other configuration

Override secrets with environment variables:

- `ConnectionStrings__Notifications`
- `RabbitMq__Host`
- `RabbitMq__Username`
- `RabbitMq__Password`
- `Resend__ApiKey`
