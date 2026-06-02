namespace ControlR.Viewer.Avalonia.Exceptions;

/// <summary>
/// Exception thrown when a viewer instance is not found in the registry.
/// </summary>
public class ViewerInstanceNotFoundException(Guid instanceId) : Exception($"Viewer instance with ID {instanceId} not found.")
{
}