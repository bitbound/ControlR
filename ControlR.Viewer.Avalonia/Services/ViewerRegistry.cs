using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Viewer.Avalonia.Services;

/// <summary>
/// Global registry for storing and retrieving ControlrViewer instances and their associated service providers.
/// </summary>
public static class ViewerRegistry
{
  private static readonly ConcurrentDictionary<Guid, ViewerInstanceInfo> _instances = new();

  /// <summary>
  /// Get all registered viewer instance IDs.
  /// </summary>
  public static ICollection<Guid> GetAllInstanceIds()
  {
    return _instances.Keys;
  }

  /// <summary>
  /// Get a required service from a specific viewer instance.
  /// </summary>
  /// <exception cref="InvalidOperationException">Thrown if the viewer instance is not found.</exception>
  public static T GetRequiredService<T>(Guid instanceId) where T : notnull
  {
    if (!TryGetInstance(instanceId, out var instance))
    {
      throw new InvalidOperationException($"Viewer instance {instanceId} not found.");
    }

    return instance.ServiceProvider.GetRequiredService<T>();
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
  /// Register a new viewer instance.
  /// </summary>
  public static void Register(Guid instanceId, ControlrViewer viewer, IServiceProvider serviceProvider)
  {
    var info = new ViewerInstanceInfo(viewer, serviceProvider);
    _instances.TryAdd(instanceId, info);
  }

  /// <summary>
  /// Get a viewer instance by ID.
  /// </summary>
  public static bool TryGetInstance(
    Guid instanceId,
    [NotNullWhen(true)] out ViewerInstanceInfo? viewerInstanceInfo)
  {
    if (_instances.TryGetValue(instanceId, out viewerInstanceInfo))
    {
      return true;
    }
    return false;
  }

  /// <summary>
  /// Unregister a viewer instance.
  /// </summary>
  public static void Unregister(Guid instanceId)
  {
    _instances.TryRemove(instanceId, out _);
  }
}