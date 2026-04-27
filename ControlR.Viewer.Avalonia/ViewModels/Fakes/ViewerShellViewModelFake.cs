using Avalonia.Controls;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace ControlR.Viewer.Avalonia.ViewModels.Fakes;

internal class ViewerShellViewModelFake : ViewModelBaseFake<ViewerShell>, IViewerShellViewModel
{
  public ViewerShellViewModelFake()
  {
    ConnectionStatus = "Connected";
    IsConnected = true;
    AlertSeverity = SnackbarSeverity.Info;
    CurrentPage = new RemoteControlView();
    CurrentViewModel = new RemoteControlViewModelFake();
    ReconnectCommand = new AsyncRelayCommand(() => Task.CompletedTask);
  }

  public event PropertyChangedEventHandler? PropertyChanged
  {
    add { }
    remove { }
  }

  public string? AlertMessage { get; set; }
  public SnackbarSeverity AlertSeverity { get; set; }
  public HubConnectionState ConnectionState => HubConnectionState.Connected;
  public string ConnectionStatus { get; set; }
  public Control? CurrentPage { get; set; }
  public IViewModelBase? CurrentViewModel { get; set; }
  public bool HasAlertMessage => !string.IsNullOrWhiteSpace(AlertMessage);
  public bool IsConnected { get; set; }
  public bool IsDeviceOffline => false;
  public IAsyncRelayCommand ReconnectCommand { get; }
  public bool ShowReconnectButton => false;

  public Task ReinitializeCurrentView()
  {
    return Task.CompletedTask;
  }
}
