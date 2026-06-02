namespace ControlR.Viewer.Avalonia.Exceptions;

/// <summary>
/// Exception thrown when a service is not found a viewer instance's service provider.
/// </summary>
public class ServiceNotFoundException<T>(Guid instanceId) 
  : Exception($"Service of type {typeof(T).Name} for viewer instance with ID {instanceId} not found.")
{
}