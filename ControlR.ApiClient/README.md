# ControlR API Client

A .NET client library for interacting with the ControlR API. Built with [Kiota](https://learn.microsoft.com/en-us/openapi/kiota/overview), this library provides a strongly-typed interface for making API calls to the backend of a ControlR server.

## Features

- Strongly-typed API client generated from OpenAPI specification
- Built-in support for dependency injection
- Static builder pattern for scenarios where dependency injection is not available
- Efficient HTTP connection management via `IHttpClientFactory`
- Automatic request/response serialization

## Installation

```bash
dotnet add package ControlR.ApiClient
```

## Quick Start

The library supports two usage patterns: dependency injection (recommended for most applications) and a static builder pattern (useful for scripts or simple scenarios).

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

Inject either `IControlrApiClientFactory` or `ControlrApiClient` directly into your services:

```csharp
using ControlR.ApiClient;

public class MyService
{
    private readonly IControlrApiClientFactory _clientFactory;
    private readonly ILogger<MyService> _logger;

    public MyService(IControlrApiClientFactory clientFactory, ILogger<MyService> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task GetDevicesAsync(CancellationToken cancellationToken)
    {
        var client = _clientFactory.GetClient();
        var devices = await client.Api.Devices.GetAsync(cancellationToken: cancellationToken);

        if (devices is null)
        {
            _logger.LogError("Response was empty.");
            return;
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
var devices = await client.Api.Devices.GetAsync(cancellationToken: cancellationToken);

if (devices is null)
{
    Console.WriteLine("Response was empty.");
    return;
}

Console.WriteLine($"Retrieved {devices.Count} devices.");
foreach (var device in devices)
{
    Console.WriteLine($" - {device.Name} (ID: {device.Id})");
}
```

## Configuration

### ControlrApiClientOptions

| Property                     | Type     | Required | Description                                       |
|------------------------------|----------|----------|---------------------------------------------------|
| `BaseUrl`                   | `Uri`    | Yes      | The base URL of your ControlR server             |
| `PersonalAccessToken`       | `string` | Yes      | Your personal access token for authentication     |

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

### Kiota Client Generation

The client is generated using Microsoft's Kiota tool, which provides:

- Type-safe request builders
- Automatic serialization/deserialization
- Fluent API design
- Built-in error handling
- Support for various authentication schemes

## Additional Resources

- [Kiota Documentation](https://learn.microsoft.com/en-us/openapi/kiota/overview)
- [Kiota .NET Dependency Injection Guide](https://learn.microsoft.com/en-us/openapi/kiota/tutorials/dotnet-dependency-injection)
- [IHttpClientFactory Best Practices](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
- [ControlR Documentation](https://github.com/bitbound/ControlR)

## Example Project

For a complete working example, see the [ControlR.ApiClientExample](../Examples/ControlR.ApiClientExample) project in this repository.

## License

This project is licensed under the [MIT License](../LICENSE.txt).