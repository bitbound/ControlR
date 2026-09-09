# ControlR API Client

A .NET client library for interacting with the ControlR API. This library provides a strongly-typed interface for making API calls to the backend of a ControlR server.

## Features

- Strongly-typed API client generated from OpenAPI specification
- Built-in support for dependency injection
- Static builder pattern for scenarios where dependency injection is not available
- Multi-server factory for backend integrations that target different servers with different credentials at runtime
- Efficient HTTP connection management via `IHttpClientFactory`
- Automatic request/response serialization
- Two authentication modes: Personal Access Token (stateless) and Interactive Bearer (email/password with automatic token refresh)
- Interactive bearer session supports two-factor authentication and password-change flows
- Session snapshot/restore for persisting tokens (e.g., caching in a secure keychain for automatic re-auth across app restarts)

## Installation

```bash
dotnet add package ControlR.ApiClient
```

## Quick Start

The library supports three usage patterns:

- **Dependency injection.** Recommended for applications that connect to a single ControlR server.
- **Static builder.** For scripts or simple scenarios without a service provider.
- **Multi-server factory.** For backend integrations that target several ControlR servers at once, each with its own base URL and credentials.

### Option 1: Dependency Injection

#### Service Registration

Configure the client in your `Program.cs` or startup file:

```csharp
using ControlR.ApiClient;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddControlrApiClient(options =>
{
    options.BaseUrl = new Uri("https://your-controlr-server.com");
    options.PersonalAccessToken = "your-personal-access-token";
});
```

You can also load options from configuration using the overload that accepts `IConfiguration`:

```csharp
using ControlR.ApiClient;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddControlrApiClient(
    builder.Configuration,
    ControlrApiClientOptions.SectionKey);
```

With the following configuration in `appsettings.json`:

```json
{
  "ControlrApiClient": {
    "BaseUrl": "https://your-controlr-server.com",
    "PersonalAccessToken": "your-personal-access-token"
  }
}
```

#### Using the Client

Inject `IControlrApi` directly into your services:

```csharp
using ControlR.ApiClient;

public class MyService
{
    private readonly IControlrApi _client;
    private readonly ILogger<MyService> _logger;

    public MyService(IControlrApi client, ILogger<MyService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task GetDevicesAsync(CancellationToken cancellationToken)
    {
        var devices = new List<DeviceResponseDto>();
        await foreach (var device in _client.Devices.GetAllDevices(cancellationToken).WithCancellation(cancellationToken))
        {
            devices.Add(device);
        }

        _logger.LogInformation("Retrieved {DeviceCount} devices.", devices.Count);
        foreach (var device in devices)
        {
            _logger.LogInformation(" - {DeviceName} (ID: {DeviceId})", device.Name, device.Id);
        }
    }
}
```

### Option 2: Static Builder

The static builder pattern is useful for console applications, scripts, or scenarios where you prefer not to use dependency injection.

#### Initialization

Initialize the builder once at application startup:

```csharp
using ControlR.ApiClient;

ControlrApiClientBuilder.Initialize(options =>
{
    options.BaseUrl = new Uri("https://your-controlr-server.com");
    options.PersonalAccessToken = "your-personal-access-token";
});
```

#### Using the Client

Get a client instance whenever needed:

```csharp
var client = ControlrApiClientBuilder.GetClient();
var devices = new List<DeviceResponseDto>();
await foreach (var device in client.Devices.GetAllDevices(cancellationToken).WithCancellation(cancellationToken))
{
    devices.Add(device);
}

Console.WriteLine($"Retrieved {devices.Count} devices.");
foreach (var device in devices)
{
    Console.WriteLine($" - {device.Name} (ID: {device.Id})");
}
```

### Option 3: Multi-Server Factory

Backend integrations that talk to several ControlR servers at once (e.g. a service account
managing a fleet of tenant servers discovered at runtime) use a factory instead of a single
singleton client. Each target gets its own base URL, credentials, token-refresh state, and
connection pool.

#### Service Registration

Server-side only. Do not use from Blazor WebAssembly; use `AddControlrApiClient` there.

```csharp
using ControlR.ApiClient;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddControlrApiClientFactory(options =>
{
    // Idle targets are evicted after 30 minutes by default; null disables eviction.
    options.MaxIdleClientLifetime = TimeSpan.FromMinutes(30);
    // Optional cap. When reached, the least-recently-used target is evicted.
    options.MaxTrackedClients = 100;
});
```

An `IConfiguration` overload (`AddControlrApiClientFactory(builder.Configuration, ControlrApiClientFactoryOptions.SectionKey)`) is also available.

#### Using the Factory

Inject `IControlrApiClientFactory` and reconcile the tracked targets against your server registry.

```csharp
public class FleetService
{
    private readonly IControlrApiClientFactory _factory;

    public FleetService(IControlrApiClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<DeviceResponseDto>> GetDevicesAsync(
        string tenantId,
        Uri baseUrl,
        string pat,
        CancellationToken cancellationToken)
    {
        // First configuration for a name wins. Pass credential updates by removing
        // and re-creating the target.
        var client = _factory.GetOrCreateClient(tenantId, options =>
        {
            options.BaseUrl = baseUrl;
            options.PersonalAccessToken = pat;
        });

        var devices = new List<DeviceResponseDto>();
        await foreach (var device in client.Internal.Devices
            .GetAllDevices(cancellationToken)
            .WithCancellation(cancellationToken))
        {
            devices.Add(device);
        }

        return devices;
    }

    public void Forget(string tenantId) => _factory.TryRemoveClient(tenantId);
}
```

To rotate a target's credentials, call `TryRemoveClient(name)` and then `GetOrCreateClient(name, newOptions)`. Interactive auth sessions come from `GetOrCreateAuthSession(name)`, created lazily so service-account-only consumers never pay for one.

See `IControlrApiClientFactory` and `ControlrApiClientFactoryOptions` for the full surface.

## Configuration

### ControlrApiClientOptions

| Property                     | Type     | Required | Description                                       |
|------------------------------|----------|----------|---------------------------------------------------|
| `BaseUrl`                   | `Uri`    | Yes      | The base URL of your ControlR server             |
| `PersonalAccessToken`       | `string` | No       | A personal access token for stateless auth (omit or leave null for interactive bearer auth) |
| `AuthenticationMethod`      | `ViewerAuthenticationMethod` | No | `PersonalAccessToken` (default) or `InteractiveBearer` |

### ControlrApiClientFactoryOptions

For `AddControlrApiClientFactory` (server-side only):

| Property                  | Type        | Default          | Description                                                                                                |
|---------------------------|-------------|------------------|------------------------------------------------------------------------------------------------------------|
| `MaxIdleClientLifetime`   | `TimeSpan?` | 30 minutes       | How long a target may go unused before the sweeper evicts it. `null` disables idle eviction.               |
| `SweeperInterval`         | `TimeSpan`  | 1 minute         | How often the sweeper checks for idle targets. Must be greater than zero.                                  |
| `MaxTrackedClients`       | `int?`      | `null`           | Maximum tracked targets. When reached, creating a new target evicts the least-recently-used one. `null` means unlimited; values below 1 are rejected at startup. |
| `HttpMessageHandlerFactory` | `Func<HttpMessageHandler>?` | `null` | Creates the primary handler per target (proxy, custom TLS, etc.). Must return a NEW instance per call. |

### Authentication

The client supports two authentication modes:

#### Personal Access Token (PAT)

Stateless — set `PersonalAccessToken` in options or call `SetPersonalAccessToken()` on the session. Works without any sign-in flow.

#### Interactive Bearer

Stateful session backed by email/password sign-in. Resolve `IControlrAuthSession` to manage the session lifecycle:

```csharp
var result = await authSession.SignIn(
  new InteractiveSignInRequest
  {
    Email = "user@example.com",
    Password = "password",
    TwoFactorCode = twoFactorCode    // only needed if 2FA is enabled for the account
  });

if (result.Status == InteractiveLoginStatus.Authenticated)
{
  // Session is ready. Tokens refresh automatically in the background.
}
```

For automatic re-auth across app restarts, cache the session snapshot and restore it:

```csharp
// Persist after sign-in
var snapshot = authSession.GetAuthSnapshot();
// Store snapshot.BearerToken, snapshot.RefreshToken, snapshot.BearerTokenExpiresAt
// in a secure keychain.

// Restore on next launch
await authSession.RestoreAuthSnapshot(snapshot);
// Session resumes with the background refresh loop.
```

For two-factor or password-change flows, check `RequiresTwoFactor` and `RequiresPasswordChange` on `IControlrAuthSession` and re-call `SignIn` with the additional fields.

### Obtaining a Personal Access Token

1. Log in to your ControlR server
2. Navigate to your account settings
3. Generate a new Personal Access Token
4. Store it securely (e.g., using User Secrets in development or secure configuration in production)

## How It Works

### IHttpClientFactory Integration

The ControlR API Client uses `IHttpClientFactory` under the hood to manage `HttpClient` instances. This provides several benefits:

- **Prevents socket exhaustion**: Automatically manages the lifecycle of HTTP connections
- **Handles DNS changes**: Respects DNS TTL by periodically recycling connections
- **Efficient resource usage**: Pools and reuses connections
- **Handler pipeline**: Supports middleware-style message handlers for cross-cutting concerns

This means you can safely create multiple client instances without worrying about common pitfalls associated with direct `HttpClient` usage.

## Example Project

For a complete working example, see the [ControlR.ApiClientExample](../Examples/ControlR.ApiClientExample) project in this repository.

## License

This project is licensed under the [MIT License](../LICENSE.txt).