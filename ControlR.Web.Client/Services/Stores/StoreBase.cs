﻿using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Client.Services.Stores;

public interface IStoreBase<TDto>
  where TDto : IHasPrimaryKey
{
  ICollection<TDto> Resources { get; }
  void AddOrUpdate(TDto device);
  void Clear();
  Task Refresh();
  Task Remove(TDto device);
  bool TryGet(Guid deviceId, [NotNullWhen(true)] out TDto? device);
}

public abstract class StoreBase<TDto>(
  IControlrApi controlrApi,
  ISnackbar snackbar,
  ILogger<StoreBase<TDto>> logger) : IStoreBase<TDto>
  where TDto : IHasPrimaryKey
{
  private readonly SemaphoreSlim _refreshLock = new(1, 1);

  protected ConcurrentDictionary<Guid, TDto> Cache { get; } = new();
  protected SemaphoreSlim RefreshLock { get; } = new(1, 1);
  protected IControlrApi ControlrApi { get; } = controlrApi;
  protected ISnackbar Snackbar { get; } = snackbar;
  protected ILogger<StoreBase<TDto>> Logger { get; } = logger;

  public ICollection<TDto> Resources => Cache.Values;

  public void AddOrUpdate(TDto device)
  {
    Cache.AddOrUpdate(device.Id, device, (_, _) => device);
  }

  public void Clear()
  {
    Cache.Clear();
  }
  
  public async Task Refresh()
  {
    if (!await _refreshLock.WaitAsync(0))
    {
      // If another thread already acquired the lock, we still want to wait
      // for it to finish, but we don't want to do another refresh.
      await _refreshLock.WaitAsync();
      _refreshLock.Release();
    }

    try
    {
      await RefreshImpl();
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

  public Task Remove(TDto device)
  {
    _ = Cache.Remove(device.Id, out _);
    return Task.CompletedTask;
  }

  public bool TryGet(Guid deviceId, [NotNullWhen(true)] out TDto? device)
  {
    return Cache.TryGetValue(deviceId, out device);
  }

  protected abstract Task RefreshImpl();
}