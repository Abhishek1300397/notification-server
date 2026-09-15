# Notification Manager

Reliable notification API using PostgreSQL outbox processing and RabbitMQ consumers.

## Run locally

```bash
docker compose up -d
dotnet restore
dotnet ef database update --project src/Notification.Infrastructure --startup-project src/Notification.Api
dotnet run --project src/Notification.Api
```

Create a notification:

```bash
curl -X POST http://localhost:5080/api/notifications \
  -H 'Content-Type: application/json' \
  -d '{
    "tenantId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "channel": "Email",
    "recipient": "user@example.com",
    "templateId": "welcome",
    "data": { "name": "Ada" }
  }'
```

## Tests

```bash
dotnet test
```

## Configuration

Override secrets with environment variables, for example:

- `ConnectionStrings__Notifications`
- `RabbitMq__Host`
- `RabbitMq__Username`
- `RabbitMq__Password`
