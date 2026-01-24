using System.Collections.ObjectModel;

namespace ControlR.DesktopClient.ViewModels.Fakes;

internal class ConnectionsViewModelFake : ViewModelBaseFake<ConnectionsView>, IConnectionsViewModel
{
  public ConnectionsViewModelFake()
  {
    ActiveSessions.AddRange(
      new RemoteControlConnection(Guid.NewGuid(), "Loki", DateTimeOffset.Now),
      new RemoteControlConnection(Guid.NewGuid(), "Thor", DateTimeOffset.Now));
  }
  public ObservableCollection<RemoteControlConnection> ActiveSessions { get; } = [];

  public IAsyncRelayCommand DisconnectCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);

  public bool IsAgentConnected { get; set; }

  public RemoteControlConnection? SelectedConnection { get; set; }
}
