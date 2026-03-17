using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ControlR.Web.Client.StateManagement.Stores;

public interface IStoreBase<TDto>
  where TDto : class
{
  IReadOnlyList<TDto> Items { get; }
  Task AddOrUpdate(TDto dto);
  Task Clear();
  Task InvokeItemsChanged();
  Task Refresh();
  IDisposable RegisterChangeHandler(object subscriber, Func<Task> handler);
  IDisposable RegisterChangeHandler(object subscriber, Action handler);
  Task<bool> Remove(TDto dto);
  Task<bool> Remove(Guid id);
  void SetItems(IEnumerable<TDto> items);
  bool TryGet(Guid id, [NotNullWhen(true)] out TDto? dto);
}

public abstract class StoreBase<TDto>(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<StoreBase<TDto>> logger) : IStoreBase<TDto>
  where TDto : class
{
  private readonly ConcurrentDictionary<Guid, TDto> _cache = new();
  private readonly ConditionalWeakTable<object, Func<Task>> _changeHandlers = [];
  private readonly SemaphoreSlim _refreshLock = new(1, 1);

  // Immutable snapshot that consumers read. Rebuilt on mutations to provide a stable, pre-sorted view.
  private ImmutableArray<TDto> _snapshot = [];

  public IReadOnlyList<TDto> Items => _snapshot;

  protected IControlrApi ControlrApi { get; } = controlrApi;
  protected ILogger<StoreBase<TDto>> Logger { get; } = logger;
  protected ISnackbar Snackbar { get; } = snackbar;

  public async Task AddOrUpdate(TDto dto)
  {
    var id = GetItemId(dto);
    if (id == Guid.Empty)
    {
      Logger.LogWarning("Cannot add or update {ResourceName} because the item has no valid primary key.", typeof(TDto).Name);
      return;
    }

    _cache.AddOrUpdate(id, dto, (_, _) => dto);
    RebuildSnapshot();
    await InvokeChangeHandlers();
  }

  public async Task Clear()
  {
    _cache.Clear();
    RebuildSnapshot();
    await InvokeChangeHandlers();
  }

  public async Task InvokeItemsChanged()
  {
    // Snapshot already kept up-to-date; just notify.
    await InvokeChangeHandlers();
  }

  public async Task Refresh()
  {
    if (!await _refreshLock.WaitAsync(0))
    {
      // If another thread already acquired the lock, we still want to wait
      // for it to finish, but we don't want to do another refresh.
      await _refreshLock.WaitAsync();
      _refreshLock.Release();
      return;
    }

    try
    {
      await RefreshImpl();
      RebuildSnapshot();
      await InvokeChangeHandlers();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while refreshing {ResourceName} store.", typeof(TDto).Name);
      Snackbar.Add($"Failed to load {typeof(TDto).Name} store", Severity.Error);
    }
    finally
    {
      _refreshLock.Release();
    }
  }

  public IDisposable RegisterChangeHandler(object subscriber, Func<Task> handler)
  {

    lock (_changeHandlers)
    {
      _changeHandlers.AddOrUpdate(subscriber, handler);
    }

    return new CallbackDisposable(() =>
    {
      lock (_changeHandlers)
      {
        _changeHandlers.Remove(subscriber);
      }
    });
  }

  public IDisposable RegisterChangeHandler(object subscriber, Action handler)
  {
    lock (_changeHandlers)
    {
      _changeHandlers.AddOrUpdate(subscriber, InvokeHandler);
    }

    return new CallbackDisposable(() =>
    {
      lock (_changeHandlers)
      {
        _changeHandlers.Remove(subscriber);
      }
    });

    Task InvokeHandler()
    {
      handler.Invoke();
      return Task.CompletedTask;
    }
  }

  public async Task<bool> Remove(TDto dto)
  {
    var id = GetItemId(dto);
    if (id == Guid.Empty)
    {
      Logger.LogWarning("Cannot remove {ResourceName} because the item has no valid primary key.", typeof(TDto).Name);
      return false;
    }

    var removed = _cache.Remove(id, out _);
    if (removed) RebuildSnapshot();
    await InvokeChangeHandlers();
    return removed;
  }

  public async Task<bool> Remove(Guid id)
  {
    var removed = _cache.Remove(id, out _);
    if (removed) RebuildSnapshot();
    await InvokeChangeHandlers();
    return removed;
  }

  public void SetItems(IEnumerable<TDto> items)
  {
    _cache.Clear();
    foreach (var dto in items)
    {
      var id = GetItemId(dto);
      if (id == Guid.Empty)
      {
        Logger.LogWarning("Cannot add {ResourceName} to store because it has no valid primary key.", typeof(TDto).Name);
        continue;
      }
      _ = _cache.TryAdd(id, dto);
    }
  }

  public bool TryGet(Guid id, [NotNullWhen(true)] out TDto? dto)
  {
    return _cache.TryGetValue(id, out dto);
  }

  protected abstract Guid GetItemId(TDto dto);

  // Override to provide ordering for the snapshot. Default: preserve enumeration order.
  protected virtual IEnumerable<TDto> OrderItems(IEnumerable<TDto> items) => items;

  protected abstract Task RefreshImpl();

  private ImmutableArray<Func<Task>> GetChangeHandlers()
  {
    lock (_changeHandlers)
    {
      return [
        .._changeHandlers.Select(x => x.Value)
      ];
    }
  }

  private async Task InvokeChangeHandlers()
  {
    foreach (var handler in GetChangeHandlers())
    {
      try
      {
        await handler.Invoke();
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Error while invoking change handler.");
      }
    }
  }

  private void RebuildSnapshot()
  {
    _snapshot = [.. OrderItems(_cache.Values)];
  }
}