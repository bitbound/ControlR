namespace ControlR.Viewer.Avalonia.Services;


/// <summary>
/// Information about a registered viewer instance.
/// </summary>
public record ViewerInstanceInfo(ControlrViewer Viewer, IServiceProvider ServiceProvider);
