namespace ControlR.Viewer.Avalonia.Services;

/// <summary>
///   Provides the instance ID for the <see cref="ControlrViewer"/> instance to which
///   the current service provider is associated.
/// </summary>
public interface IInstanceIdProvider
{
  /// <summary>
  ///   The unique identifier for the viewer instance.
  /// </summary>
  Guid InstanceId { get; }
}

internal class InstanceIdProvider(Guid instanceId) : IInstanceIdProvider
{
  public Guid InstanceId { get; } = instanceId;
}
