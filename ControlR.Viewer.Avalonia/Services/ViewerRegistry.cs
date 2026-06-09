using ControlR.ApiClient;
using ControlR.Viewer.Avalonia.Exceptions;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Collections;

namespace ControlR.Viewer.Avalonia.Services;

/// <summary>
/// Global registry for storing and retrieving ControlrViewer instances and their associated service providers.
/// </summary>
public static class ViewerRegistry
{
  private static readonly ConcurrentDictionary<Guid, InstanceEntry> _entries = new();
  private static readonly Lock _handlersLock = new();

  /// <summary>
  /// Get all registered viewer instance IDs.
  /// </summary>
  public static ICollection<Guid> GetAllInstanceIds()
  {
    return _entries.Keys;
  }

  /// <summary>
  /// Get the authentication session for a specific viewer instance.
  /// </summary>
  /// <param name="viewerInstanceId">
  /// The ID of the viewer instance for which to get the authentication session.
  /// </param>
  public static IControlrAuthSession GetAuthSession(Guid viewerInstanceId)
  {
    if (!TryGetInstance(viewerInstanceId, out var instance))
    {
      throw new ViewerInstanceNotFoundException(viewerInstanceId);
    }
    return instance.ServiceProvider.GetService<IControlrAuthSession>()
      ?? throw new ServiceNotFoundException<IControlrAuthSession>(viewerInstanceId);
  }

  /// <summary>
  /// Get a required service from a specific viewer instance.
  /// </summary>
  /// <exception cref="ViewerInstanceNotFoundException">Thrown if the viewer instance is not found.</exception>
  /// <exception cref="ServiceNotFoundException{T}">Thrown if the service is not found.</exception>
  public static T GetRequiredService<T>(Guid instanceId) where T : notnull
  {
    if (!TryGetInstance(instanceId, out var instance))
    {
      throw new ViewerInstanceNotFoundException(instanceId);
    }

    return instance.ServiceProvider.GetService<T>()
      ?? throw new ServiceNotFoundException<T>(instanceId);
  }

  /// <summary>
  /// Get a service from a specific viewer instance.
  /// </summary>
  /// <returns>
  ///   If found, returns an instance of the specified service type.
  ///   If not found, returns null.
  /// </returns>
  public static T? GetService<T>(Guid instanceId) where T : class
  {
    if (!TryGetInstance(instanceId, out var instance))
    {
      return null;
    }
    return instance.ServiceProvider.GetService(typeof(T)) as T;
  }

  /// <summary>
  /// Get a service from a specific viewer instance.
  /// </summary>
  /// <returns>
  ///   If found, returns an instance of the specified service type.
  ///   If not found, returns null.
  /// </returns>
  public static object? GetService(Guid instanceId, Type serviceType)
  {
    if (!TryGetInstance(instanceId, out var instance))
    {
      return null;
    }
    return instance.ServiceProvider.GetService(serviceType);
  }

  /// <summary>
  /// Register a callback that runs when a specific viewer instance's services are ready for use.
  /// If the instance is already registered, the handler runs immediately.
  /// </summary>
  public static IDisposable OnServicesReady(Guid instanceId, Func<ViewerInstanceInfo, Task> handler)
  {
    ArgumentNullException.ThrowIfNull(handler);

    lock (_handlersLock)
    {
      if (!_entries.TryGetValue(instanceId, out var entry) || entry.Info is null)
      {
        var newEntry = new InstanceEntry();
        newEntry.Handlers.Add(handler);
        _entries.TryAdd(instanceId, newEntry);
        return new CallbackDisposable(() =>
        {
          RemoveHandler(instanceId, handler);
        });
      }

      NotifyServicesReady(entry.Info, handler).Forget();
    }

    return new NoopDisposable();
  }

  /// <summary>
  /// Register a new viewer instance.
  /// </summary>
  public static ViewerInstanceInfo Register(Guid instanceId, ControlrViewer viewer, IServiceProvider serviceProvider)
  {
    var info = new ViewerInstanceInfo(instanceId, viewer, serviceProvider);
    List<Func<ViewerInstanceInfo, Task>>? handlersToNotify;

    lock (_handlersLock)
    {
      if (!_entries.TryGetValue(instanceId, out var entry))
      {
        entry = new InstanceEntry();
        _entries.TryAdd(instanceId, entry);
      }
      else if (entry.Info is not null)
      {
        return entry.Info;
      }

      entry.Info = info;
      handlersToNotify = entry.Handlers.Count > 0 ? [.. entry.Handlers] : null;
      entry.Handlers.Clear();
    }

    if (handlersToNotify is not null)
    {
      NotifyServicesReady(info, handlersToNotify).Forget();
    }

    return info;
  }

  /// <summary>
  /// Get a viewer instance by ID.
  /// </summary>
  public static bool TryGetInstance(
      Guid instanceId,
      [NotNullWhen(true)] out ViewerInstanceInfo? viewerInstanceInfo)
  {
    if (_entries.TryGetValue(instanceId, out var entry))
    {
      viewerInstanceInfo = entry.Info;
      return viewerInstanceInfo is not null;
    }
    viewerInstanceInfo = null;
    return false;
  }

  /// <summary>
  /// Unregister a viewer instance.
  /// </summary>
  public static void Unregister(Guid instanceId)
  {
    _ = _entries.TryRemove(instanceId, out _);
  }

  private static async Task NotifyServicesReady(
    ViewerInstanceInfo instanceInfo,
    IReadOnlyList<Func<ViewerInstanceInfo, Task>> handlers)
  {
    foreach (var handler in handlers)
    {
      await NotifyServicesReady(instanceInfo, handler);
    }
  }

  private static async Task NotifyServicesReady(
    ViewerInstanceInfo instanceInfo,
    Func<ViewerInstanceInfo, Task> handler)
  {
    ILogger<ViewerInstanceInfo>? logger = null;
    try
    {
      logger = instanceInfo.ServiceProvider.GetService<ILogger<ViewerInstanceInfo>>();
      await handler(instanceInfo);
    }
    catch (Exception ex)
    {
      logger?.LogError(ex, "Error executing services ready handler for viewer instance {InstanceId}", instanceInfo.InstanceId);
    }
  }

  private static void RemoveHandler(Guid instanceId, Func<ViewerInstanceInfo, Task> handler)
  {
    lock (_handlersLock)
    {
      if (!_entries.TryGetValue(instanceId, out var entry) || entry.Handlers.Count == 0)
      {
        return;
      }

      entry.Handlers.Remove(handler);
      if (entry.Handlers.Count == 0)
      {
        _ = _entries.TryRemove(instanceId, out _);
      }
    }
  }

  private class InstanceEntry
  {
    public ConcurrentList<Func<ViewerInstanceInfo, Task>> Handlers { get; } = [];
    public ViewerInstanceInfo? Info { get; set; }
  }
}