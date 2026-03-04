# ControlR.Libraries.Shared

A comprehensive shared library providing common utilities, extensions, collections, primitives, and services used across all ControlR applications.

## Features

### Collections

- **ConcurrentHashSet**: Thread-safe hash set implementation
- **ConcurrentList**: Thread-safe list implementation
- **DisposableCollection**: Collection of disposable objects
- **HandlerCollection**: Collection of event handlers with error handling

### Converters

- **TimeSpanJsonConverter**: JSON converter for TimeSpan values

### Extensions

#### CancellationToken Extensions

- Extensions for working with CancellationToken

#### Cloning Extensions

- Deep cloning utilities for objects

#### Collection Extensions

- LINQ-style extensions for collections

#### DateTime Extensions

- DateTime manipulation and formatting utilities

#### IDisposable Extensions

- Helper methods for disposable objects

#### ILogger Extensions

- Structured logging enhancements

#### List Extensions

- Additional list manipulation methods

#### Lock Extensions

- Synchronization helpers

#### Result Extensions

- Result type operations

#### ServiceCollection Extensions

- Dependency injection registration helpers

#### String Extensions

- String manipulation utilities

#### Task Extensions

- Async/await helpers

#### Timer Extensions

- Timer-related utilities

#### Uri Extensions

- URI manipulation helpers

### Helpers

- **AppendableStream**: Stream that supports appending
- **Debouncer**: Debounce utility for rate-limiting
- **DeterministicGuid**: Deterministic GUID generation
- **Disposer**: IDisposable helper
- **Guard**: Argument validation
- **IoHelper**: I/O operations helper
- **JsonValueFilter**: JSON filtering utility
- **MathHelper**: Mathematical utilities
- **NoopDisposable**: No-op disposable implementation
- **OptionsMonitorWrapper**: Options monitoring wrapper
- **RandomGenerator**: Random value generation
- **RateLimiter**: Rate limiting utility
- **TryHelper**: Try-catch helper
- **UnitsHelper**: Unit conversion utilities

### IO

- **CompoundReadStream**: Combined stream reader
- **ReactiveFileStream**: Reactive file stream
- **StreamObserver**: Stream observation utility

### Logging

- **LogDeduplicationContext**: Log deduplication for repeated messages

### Primitives

- **AutoResetEventAsync**: Async AutoResetEvent
- **CallbackDisposable**: Callback-based disposable
- **CallbackDisposableAsync**: Async callback-based disposable
- **DisposableValue**: Disposable wrapper for values
- **ManualResetEventAsync**: Async ManualResetEvent
- **Result**: Result type for error handling
- **ScopedGuard**: Scoped guard implementation
- **ScopedLock**: Scoped locking utility

### Services

- **EmbeddedResourceAccessor**: Access embedded resources
- **LazyInjector**: Lazy dependency injection
- **Retryer**: Retry logic with exponential backoff
- **SystemEnvironment**: System environment information
- **Waiter**: Wait/notification helpers

#### Buffers

- **ArrayPoolOwner**: Array pool wrapper
- **EphemeralBuffer**: Ephemeral buffer management
- **MemoryProvider**: Memory allocation helpers
- **PooledMemoryOwner**: Pooled memory wrapper
- **SlicedMemoryOwner**: Sliced memory wrapper

#### HTTP

- **DownloadsApi**: Download utilities

#### State Management

- **ObservableState**: Observable state for state management

## Usage

### Collections

```csharp
using ControlR.Libraries.Shared.Collections;

var hashSet = new ConcurrentHashSet<string>();
hashSet.Add("item");

var list = new ConcurrentList<int>();
list.Add(1);
```

### Result Type

```csharp
using ControlR.Libraries.Shared.Primitives;

public Result<int> Divide(int a, int b)
{
    if (b == 0)
        return Result.Fail("Division by zero");
    
    return Result.Ok(a / b);
}

var result = Divide(10, 2);
if (result.IsSuccess)
{
    Console.WriteLine(result.Value);
}
```

### Retryer

```csharp
using ControlR.Libraries.Shared.Services;

var retryer = new Retryer(logger);
await retryer.ExecuteAsync(async () =>
{
    await SomeUnreliableOperation();
}, maxAttempts: 3);
```

### Extensions

```csharp
using ControlR.Libraries.Shared.Extensions;

// Task extensions
await task.WithCancellation(token);

// String extensions
var formatted = "hello".ToTitleCase();

// Collection extensions
var item = list.FirstOrDefault(predicate);
```

## Architecture

The library is organized into multiple namespaces:

- `ControlR.Libraries.Shared.Collections` - Thread-safe collections
- `ControlR.Libraries.Shared.Constants` - Application constants
- `ControlR.Libraries.Shared.Converters` - JSON converters
- `ControlR.Libraries.Shared.Extensions` - Extension methods
- `ControlR.Libraries.Shared.Helpers` - Utility helpers
- `ControlR.Libraries.Shared.IO` - I/O utilities
- `ControlR.Libraries.Shared.Logging` - Logging utilities
- `ControlR.Libraries.Shared.Primitives` - Primitive types and helpers
- `ControlR.Libraries.Shared.Services` - Common services

## Dependencies

This library depends on:

- Microsoft.Extensions.Logging.Abstractions
- System.Text.Json
- System.Threading
