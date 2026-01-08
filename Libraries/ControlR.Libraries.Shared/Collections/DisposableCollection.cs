using System.Collections;
using System.Collections.Generic;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Libraries.Shared.Collections;

public sealed class DisposableCollection : IDisposable, IEnumerable<IDisposable>
{
  private readonly Lock _collectionLock = new();
  private readonly List<IDisposable> _disposables = [];

  private bool _isDisposed = false;

  public void Add(IDisposable disposable)
  {
    using var lockScope = _collectionLock.EnterScope();
    ObjectDisposedException.ThrowIf(_isDisposed, this);
    _disposables.Add(disposable);
  }

  public void AddRange(params IDisposable[] disposables)
  {
    using var lockScope = _collectionLock.EnterScope();
    ObjectDisposedException.ThrowIf(_isDisposed, this);
    _disposables.AddRange(disposables);
  }

  public void Dispose()
  {
    using var lockScope = _collectionLock.EnterScope();
    if (_isDisposed) return;
    Disposer.DisposeAll(_disposables);
    _disposables.Clear();
    _isDisposed = true;
  }

  public IEnumerator<IDisposable> GetEnumerator()
  {
    IDisposable[]? snapshot;
    using (var lockScope = _collectionLock.EnterScope())
    {
      ObjectDisposedException.ThrowIf(_isDisposed, this);
      snapshot = [.. _disposables];
    }

    for (var i = 0; i < snapshot.Length; i++)
      yield return snapshot[i];
  }

  public void Remove(IDisposable disposable)
  {
    using var lockScope = _collectionLock.EnterScope();
    ObjectDisposedException.ThrowIf(_isDisposed, this);
    _disposables.Remove(disposable);
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}