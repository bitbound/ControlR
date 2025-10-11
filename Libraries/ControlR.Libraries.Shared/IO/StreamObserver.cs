using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Libraries.Shared.IO;

public class StreamObserver : IAsyncDisposable
{
  private readonly CancellationTokenSource _cts = new();
  private readonly TimeSpan _observationInterval;
  private readonly Stream _observedStream;
  private readonly ConcurrentList<Func<long, Task>> _onLengthChangedHandlers = [];
  private readonly ConcurrentList<Func<long, Task>> _onPositionChangedHandlers = [];
  private long _lastObservedLength = 0;
  private long _lastObservedPosition = 0;
  private Task? _observationTask;

  public StreamObserver(Stream observedStream, TimeSpan? observationInterval = null)
  {
    _observedStream = observedStream;
    _observationInterval = observationInterval ?? TimeSpan.FromMilliseconds(100);
    StartObservation();
  }

  public async ValueTask DisposeAsync()
  {
    if (_observationTask is not null)
    {
      await _cts.CancelAsync();
      try
      {
        await _observationTask;
      }
      catch (OperationCanceledException)
      {
        // Expected
      }
      _observationTask = null;
    }
    _cts.Dispose();
    GC.SuppressFinalize(this);
  }

  public IDisposable OnLengthChanged(Func<long, Task> callback)
  {
    _onLengthChangedHandlers.Add(callback);
    return new CallbackDisposable(() => _onLengthChangedHandlers.Remove(callback));
  }

  public IDisposable OnPositionChanged(Func<long, Task> callback)
  {
    _onPositionChangedHandlers.Add(callback);
    return new CallbackDisposable(() => _onPositionChangedHandlers.Remove(callback));
  }

  private async Task Observe()
  {
    while (!_cts.Token.IsCancellationRequested)
    {
      try
      {
        if (_onPositionChangedHandlers.Count > 0)
        {
          var currentPosition = _observedStream.Position;
          if (currentPosition != _lastObservedPosition)
          {
            var handlerTasks = _onPositionChangedHandlers.Select(handler => handler(currentPosition));
            await Task.WhenAll(handlerTasks);
            _lastObservedPosition = currentPosition;
          }
        }

        if (_onLengthChangedHandlers.Count > 0)
        {
          var currentLength = _observedStream.Length;
          if (currentLength != _lastObservedLength)
          {
            var handlerTasks = _onLengthChangedHandlers.Select(handler => handler(currentLength));
            await Task.WhenAll(handlerTasks);
            _lastObservedLength = currentLength;
          }
        }
      }
      catch (ObjectDisposedException)
      {
        // Stream was closed. Stop observing.
        break;
      }
      catch (Exception)
      {
        // Ignore other exceptions and continue observing.
      }

      await Task.Delay(_observationInterval, _cts.Token);
    }
  }

  private void StartObservation()
  {
    try
    {
      if (_observedStream.CanSeek)
      {
        _lastObservedPosition = _observedStream.Position;
      }
      _lastObservedLength = _observedStream.Length;
    }
    catch (NotSupportedException)
    {
      // Some streams may not support getting Length or Position initially.
    }

    _observationTask = Task.Run(Observe, _cts.Token);
  }
}