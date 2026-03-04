# ControlR.Libraries.Signalr.Client

A strongly-typed SignalR client library that provides dynamic proxy generation for hub interfaces, simplifying client-server communication in ControlR applications.

## Features

### Strongly-Typed Hub Connection

- **IHubConnection<T>**: Generic interface providing strongly-typed access to hub methods
- **HubConnection**: Base implementation with connection lifecycle management

### Dynamic Proxy Generation

- **HubProxyGenerator**: Generates dynamic proxies for hub interfaces at runtime
- **ProxyInvocationHandler**: Handles method invocations on generated proxies

### Diagnostics

- **DefaultActivitySource**: Activity tracing support for distributed tracing

### Error Handling

- **DynamicObjectGenerationException**: Exception thrown when proxy generation fails

### Extension Methods

Helper methods for common SignalR operations and dependency injection registration.

## Usage

### Basic Usage

```csharp
using ControlR.Libraries.Signalr.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Define your hub interface
public interface IMyHub
{
    Task<string> GetMessage();
    Task SendMessage(string message);
    Task<string> GetUserData(int userId);
    
    // Client-invokable methods
    Task OnServerMessage(string message);
}

// Register services
var services = new ServiceCollection();
services.AddLogging();
services.AddSignalRHubConnection<IMyHub>(hubUri);

var provider = services.BuildServiceProvider();

// Get the hub connection
var hubConnection = provider.GetRequiredService<IHubConnection<IMyHub>>();

// Connect
await hubConnection.ConnectAsync();

// Call hub methods
var message = await hubConnection.GetMessage();
await hubConnection.SendMessage("Hello, Server!");

// Register for server-invoked methods
hubConnection.OnServerMessage(message =>
{
    Console.WriteLine($"Received: {message}");
});
```

### With Custom Options

```csharp
services.AddSignalRHubConnection<IMyHub>(options =>
{
    options.Url = "https://my-server/hub";
    options.AccessTokenProvider = () => Task.FromResult(myToken);
    options.Headers["X-Custom-Header"] = "value";
});
```

### Manual Creation

```csharp
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<HubConnection>();

var hubConnection = new HubConnection<IMyHub>(
    hubUri,
    loggerFactory,
    logger);

// Configure additional options
hubConnection.Closed += async (exception) =>
{
    // Handle connection closed
    await Task.CompletedTask;
};

hubConnection.Reconnected += async (connectionId) =>
{
    // Handle reconnection
    await Task.CompletedTask;
};

await hubConnection.ConnectAsync();
```

## Interface Definition

When defining your hub interface, include both server methods (that the client calls) and client methods (that the server invokes):

```csharp
public interface IViewerHub
{
    // Server methods - client invokes these
    Task<bool> AuthorizeViewer(string token);
    Task<RemoteControlSessionDto> StartRemoteControl(Guid deviceId, ControlMode mode);
    Task StopRemoteControl();
    IAsyncEnumerable<byte[]> GetScreenStream(CancellationToken cancellationToken);
    
    // Client methods - server invokes these
    Task OnDeviceUpdate(DeviceUpdateDto dto);
    Task OnConnectionStateChanged(ConnectionStateDto state);
}
```

## Architecture

The library is organized as follows:

- `HubConnection.cs` - Main hub connection interface and implementation
- `Diagnostics/` - Distributed tracing support
- `Exceptions/` - Custom exception types
- `Extensions/` - Service collection and connection extensions
- `Internals/` - Proxy generation infrastructure
  - `HubProxyGenerator.cs` - Dynamic proxy generation
  - `IInvocationHandler.cs` - Invocation handler interface
  - `ProxyInvocationHandler.cs` - Proxy invocation implementation

## Key Interfaces

### IHubConnection<T>

The main interface providing:

- `ConnectAsync()` / `DisconnectAsync()` - Connection lifecycle
- Server method proxies - Strongly-typed method calls
- `On<T>(Action<T>)` - Register handlers for server-invoked methods
- Event handlers for connection state changes
- Streaming support for hub methods

## Dependencies

This library depends on:

- Microsoft.AspNetCore.SignalR.Client
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging
- System.Reflection.Emit
