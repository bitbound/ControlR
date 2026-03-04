# ControlR.Libraries.WebSocketRelay.Client

A client library for the WebSocket Relay service, providing managed relay stream connections for real-time data transfer in ControlR remote control applications.

## Features

### Managed Relay Stream

- **IManagedRelayStream**: Interface for managing WebSocket relay connections
- **ManagedRelayStream**: Abstract base implementation with connection management

### Connection Management

- Automatic connection handling and reconnection
- Message queue management with configurable buffering
- Close handler registration
- Latency tracking

### Performance Monitoring

- **CurrentLatency**: Real-time latency measurement
- **GetMbpsIn()**: Inbound throughput monitoring
- **GetMbpsOut()**: Outbound throughput monitoring

### Message Handling

- DTO wrapper support with MessagePack serialization
- Message handler registration for incoming messages
- Async message processing

## Usage

### Basic Usage

```csharp
using ControlR.Libraries.WebSocketRelay.Client;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using Microsoft.Extensions.Logging;

// Create a concrete implementation or use in your application
public class MyRelayStream : ManagedRelayStream
{
    public MyRelayStream(
        TimeProvider timeProvider,
        IMessenger messenger,
        IMemoryProvider memoryProvider,
        IWaiter waiter,
        ILogger<ManagedRelayStream> logger)
        : base(timeProvider, messenger, memoryProvider, waiter, logger)
    {
    }
}

// Connect to the relay
var relayStream = new MyRelayStream(
    TimeProvider.System,
    messenger,
    memoryProvider,
    waiter,
    logger);

var relayUri = new Uri("wss://relay.example.com/relay");
await relayStream.Connect(relayUri, cancellationToken);

// Check connection status
Console.WriteLine($"Connected: {relayStream.IsConnected}");
Console.WriteLine($"State: {relayStream.State}");
Console.WriteLine($"Latency: {relayStream.CurrentLatency}");
```

### Sending Messages

```csharp
// Create a DTO wrapper
var dtoWrapper = new DtoWrapper
{
    DtoType = nameof(RemoteControlFrameDto),
    Data = myFrameDto
};

// Send with cancellation support
await relayStream.Send(dtoWrapper, cancellationToken);
```

### Receiving Messages

```csharp
// Register a message handler
var subscription = relayStream.RegisterMessageHandler(this, async wrapper =>
{
    switch (wrapper.DtoType)
    {
        case nameof(RemoteControlFrameDto):
            var frame = MessagePackSerializer.Deserialize<RemoteControlFrameDto>(wrapper.Data);
            await HandleFrame(frame);
            break;
            
        case nameof(InputEventDto):
            var input = MessagePackSerializer.Deserialize<InputEventDto>(wrapper.Data);
            await HandleInput(input);
            break;
    }
});

// Don't forget to dispose when done
subscription.Dispose();
```

### Monitoring Performance

```csharp
// Get throughput metrics
double inboundMbps = relayStream.GetMbpsIn();
double outboundMbps = relayStream.GetMbpsOut();

Console.WriteLine($"In: {inboundMbps:F2} Mbps");
Console.WriteLine($"Out: {outboundMbps:F2} Mbps");
```

### Handling Connection Close

```csharp
// Register a close handler
var closeSubscription = relayStream.OnClosed(async () =>
{
    Console.WriteLine("Connection closed");
    // Handle reconnection logic
});

// Dispose when done
closeSubscription.Dispose();
```

### URI Building

Use `RelayUriBuilder` to construct relay URIs:

```csharp
var uri = RelayUriBuilder.Build(
    relayServer: "relay.example.com",
    deviceId: deviceId,
    sessionId: sessionId,
    role: RelayRole.Requester);  // or RelayRole.Provider
```

## Relay Roles

- **Requester**: The client that requests a relay connection (typically the viewer)
- **Provider**: The client that provides the connection (typically the agent)

## Architecture

The library consists of:

- `ManagedRelayStream.cs` - Main stream management implementation
- `RelayRole.cs` - Enum for relay participant roles
- `RelayUriBuilder.cs` - URI construction helper

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsConnected` | `bool` | Whether the WebSocket is currently connected |
| `State` | `WebSocketState` | The current WebSocket state |
| `CurrentLatency` | `TimeSpan` | Current round-trip latency |

### Key Methods

| Method | Description |
|--------|-------------|
| `Connect()` | Establish WebSocket connection |
| `Send()` | Send a DTO wrapper message |
| `RegisterMessageHandler()` | Register handler for incoming messages |
| `OnClosed()` | Register handler for connection close events |
| `Close()` | Gracefully close the connection |
| `GetMbpsIn()` | Get inbound throughput |
| `GetMbpsOut()` | Get outbound throughput |

## Dependencies

This library depends on:

- ControlR.Libraries.Shared
- ControlR.Libraries.Api.Contracts
- Bitbound.SimpleMessenger
- Microsoft.Extensions.Logging
- MessagePack
- System.Net.WebSockets

## Use Cases

- Remote desktop viewing and control
- Real-time screen streaming
- Input event relay
- File transfer through relay
- Terminal session tunneling
