namespace ControlR.DesktopClient.ViewModels.Internals;

internal record RemoteControlConnection(
  Guid SessionId, 
  string ViewerName,
  DateTimeOffset ConnectedAt);