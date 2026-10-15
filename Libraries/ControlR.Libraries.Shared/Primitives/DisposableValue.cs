using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Libraries.Shared.Primitives;

public sealed class DisposableValue<T>(
  T value,
  Action<T>? disposeCallback = null,
  ILogger? logger = null) : IDisposable
{
  private readonly Action<T>? _disposeCallback = disposeCallback;
  private readonly ILogger _logger = logger ?? NullLogger.Instance;

  public T Value { get; } = value;

  public void Dispose()
  {
    try
    {
      _disposeCallback?.Invoke(Value);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking the dispose callback for {ValueType}.", typeof(T).Name);
    }
  }
}