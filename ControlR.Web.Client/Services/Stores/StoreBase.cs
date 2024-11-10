﻿using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ControlR.Web.Client.Services.Stores;

public interface IStoreBase<TDto>
  where TDto : IHasPrimaryKey
{
  ICollection<TDto> Items { get; }
  Task AddOrUpdate(TDto device);
  Task Clear();
  Task InvokeItemsChanged();
  Task Refresh();
  IDisposable RegisterChangeHandler(object subscriber, Func<Task> handler);
  IDisposable RegisterChangeHandler(object subscriber, Action handler);
  Task<bool> Remove(TDto device);
  bool TryGet(Guid deviceId, [NotNullWhen(true)] out TDto? device);
}

public abstract class StoreBase<TDto>(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<StoreBase<TDto>> logger) : IStoreBase<TDto>
  where TDto : IHasPrimaryKey
{
  private readonly ConditionalWeakTable<object, Func<Task>> _changeHandlers = new();
  private readonly SemaphoreSlim _refreshLock = new(1, 1);

  // TODO: Add observability.
  public ICollection<TDto> Items => Cache.Values;
  protected ConcurrentDictionary<Guid, TDto> Cache { get; } = new();
  protected IControlrApi ControlrApi { get; } = controlrApi;
  protected ILogger<StoreBase<TDto>> Logger { get; } = logger;
  protected ISnackbar Snackbar { get; } = snackbar;

  public async Task AddOrUpdate(TDto device)
  {
    Cache.AddOrUpdate(device.Id, device, (_, _) => device);
    await InvokeChangeHandlers();
  }

  public async Task Clear()
  {
    Cache.Clear();
    await InvokeChangeHandlers();
  }

  public async Task InvokeItemsChanged()
  {
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
      await InvokeChangeHandlers();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while refreshing {ResourceName} store.", nameof(TDto));
      Snackbar.Add($"Failed to load {nameof(TDto)} store", Severity.Error);
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

  public async Task<bool> Remove(TDto device)
  {
    var removed =  Cache.Remove(device.Id, out _);
    await InvokeChangeHandlers();
    return removed;
  }

  public bool TryGet(Guid deviceId, [NotNullWhen(true)] out TDto? device)
  {
    return Cache.TryGetValue(deviceId, out device);
  }

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
}