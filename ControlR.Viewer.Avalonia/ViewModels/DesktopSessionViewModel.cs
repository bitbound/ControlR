using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ControlR.Viewer.Avalonia.ViewModels.Controls;

public interface IDesktopSessionViewModel
{
  event EventHandler<DesktopSession>? PreviewRequested;
  event EventHandler<DesktopSession>? ConnectRequested;
  event EventHandler<DesktopSession>? RemoteControlPermissionRequested;

  bool AreRemoteControlPermissionsGranted { get; }
  IRelayCommand ConnectCommand { get; }
  string Name { get; }
  IRelayCommand PreviewCommand { get; }
  int ProcessId { get; }
  IRelayCommand RequestPermissionsCommand { get; }
  DesktopSession Session { get; }
  int SystemSessionId { get; }
  DesktopSessionType Type { get; }
  string Username { get; }
}

public partial class DesktopSessionViewModel : ObservableObject, IDesktopSessionViewModel
{
  private readonly DesktopSession _session;

  public DesktopSessionViewModel(DesktopSession session)
  {
    _session = session;
  }

  public event EventHandler<DesktopSession>? PreviewRequested;
  public event EventHandler<DesktopSession>? ConnectRequested;
  public event EventHandler<DesktopSession>? RemoteControlPermissionRequested;

  public bool AreRemoteControlPermissionsGranted => _session.AreRemoteControlPermissionsGranted;
  public string Name => _session.Name;
  public int ProcessId => _session.ProcessId;
  public DesktopSession Session => _session;
  public int SystemSessionId => _session.SystemSessionId;
  public DesktopSessionType Type => _session.Type;
  public string Username => _session.Username;

  [RelayCommand]
  private void Connect()
  {
    ConnectRequested?.Invoke(this, _session);
  }

  [RelayCommand]
  private void Preview()
  {
    PreviewRequested?.Invoke(this, _session);
  }

  [RelayCommand]
  private void RequestPermissions()
  {
    RemoteControlPermissionRequested?.Invoke(this, _session);
  }
}
