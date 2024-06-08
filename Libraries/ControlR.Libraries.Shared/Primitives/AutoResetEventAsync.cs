using System.Collections.Concurrent;

namespace ControlR.Libraries.Shared.Primitives;
public sealed class AutoResetEventAsync : IDisposable
{
    private readonly object _lock = new();
    private readonly ConcurrentQueue<TaskCompletionSource> _queue = new();
    private bool _isDisposed;
    private bool _isSet;
    public AutoResetEventAsync(bool isSet = false)
    {
        if (isSet)
        {
            Set();
        }
    }

    public bool IsSet
    {
        get
        {
            lock (_lock)
            {
                return _isSet;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _isDisposed = true;

            while (_queue.TryDequeue(out var tcs))
            {
                tcs.SetCanceled();
            }
        }
    }

    public void Set()
    {
        lock (_lock)
        {
            if (_queue.IsEmpty)
            {
                _isSet = true;
            }
            else
            {
                if (_queue.TryDequeue(out var tcs))
                {
                    tcs.SetResult();
                }
            }
        }
    }

    public Task Wait(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_isSet)
            {
                _isSet = false;
                return Task.CompletedTask;
            }
            else
            {
                var tsc = new TaskCompletionSource();
                _queue.Enqueue(tsc);
                return tsc.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
