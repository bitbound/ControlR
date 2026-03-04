# ControlR.Libraries.Messenger.Extensions

Extensions and message types for the Bitbound.SimpleMessenger library, providing enhanced messaging capabilities for ControlR applications.

## Features

### Message Types

- **EventMessage**: A simple message for broadcasting events with a unique event kind identifier
- **DtoReceivedMessage**: Generic message wrapper for DTOs received from hubs
- **ToastMessage**: Message type for toast notifications with severity levels

### Extension Methods

The library provides extension methods for `IMessenger` to simplify event registration and sending:

#### Registering Event Handlers

```csharp
// Register for a specific event by Guid
messenger.RegisterEvent(recipient, eventGuid, () =>
{
    // Handle the event
});

// Register with async handler
messenger.RegisterEvent(recipient, eventGuid, async () =>
{
    await SomeAsyncOperation();
});

// Register with subscriber context
messenger.RegisterEvent(recipient, (subscriber, eventKind) =>
{
    // Handle with subscriber context
});

// Register with async subscriber context
messenger.RegisterEvent(recipient, async (subscriber, eventKind) =>
{
    await SomeAsyncOperation();
});
```

#### Sending Events

```csharp
// Send an event with a specific event kind
await messenger.SendEvent(eventGuid);
```

## Usage

### Basic Usage

```csharp
using ControlR.Libraries.Messenger.Extensions;

// Define a unique event Guid
public static class MyEvents
{
    public static readonly Guid UserLoggedIn = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DataRefreshed = Guid.Parse("00000000-0000-0000-0000-000000000002");
}

// Register for events
var subscription = messenger.RegisterEvent(this, MyEvents.UserLoggedIn, () =>
{
    Console.WriteLine("User logged in!");
});

// Send an event
await messenger.SendEvent(MyEvents.UserLoggedIn);

// Don't forget to dispose when done
subscription.Dispose();
```

### Using DtoReceivedMessage

```csharp
// The DtoReceivedMessage<T> is used to broadcast DTOs
// that were received from SignalR hubs to multiple subscribers

// Register to receive a specific DTO type
messenger.Register<DtoReceivedMessage<DeviceDto>>(this, async (recipient, message) =>
{
    var device = message.Data;
    // Handle the device data
    await Task.CompletedTask;
});

// Send a DTO to all subscribers
await messenger.Send(new DtoReceivedMessage<DeviceDto>(myDeviceDto));
```

### Using ToastMessage

```csharp
// Create a toast message
var toast = new ToastMessage("Operation completed successfully", MessageSeverity.Success);

// Send the toast
await messenger.Send(toast);
```

## Architecture

The library is organized as follows:

- `IMessengerExtensions.cs` - Extension methods for `IMessenger`
- `Messages/` - Message type definitions
  - `EventMessage.cs` - Event message with Guid
  - `DtoReceivedMessage.cs` - Generic DTO wrapper
  - `ToastMessage.cs` - Toast notification message
