namespace ControlR.Web.Client.Services.DeviceAccess;

public interface IRemoteControlState
{
  DisplayDto[]? DisplayData { get; set; }
  DisplayDto? SelectedDisplay { get; set; }
  RemoteControlSession? CurrentSession { get; set; }
  IDisposable? ConnectionClosedRegistration { get; set; }
}

internal class RemoteControlState : IRemoteControlState
{
  public DisplayDto[]? DisplayData { get; set; }

  public DisplayDto? SelectedDisplay { get; set; }

  public RemoteControlSession? CurrentSession { get; set; }

  public IDisposable? ConnectionClosedRegistration { get; set; }
}
