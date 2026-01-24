using Bitbound.SimpleMessenger;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Messages;
using ControlR.DesktopClient.Models;
using ControlR.DesktopClient.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace ControlR.DesktopClient.ViewModels;

internal interface IConnectionsViewModel : IViewModelBase
{
  ObservableCollection<RemoteControlConnection> ActiveSessions { get; }
  IAsyncRelayCommand DisconnectCommand { get; }
  bool IsAgentConnected { get; }
  RemoteControlConnection? SelectedConnection { get; set; }
}

internal partial class ConnectionsViewModel(
  IRemoteControlHostManager hostManager,
  IDialogProvider dialogProvider,
  IMessenger messenger,
  IIpcClientAccessor ipcAccessor,
  ILogger<ConnectionsViewModel> logger) : ViewModelBase<ConnectionsView>, IConnectionsViewModel
{
  private readonly IDialogProvider _dialogProvider = dialogProvider;
  private readonly IRemoteControlHostManager _hostManager = hostManager;
  private readonly IIpcClientAccessor _ipcAccessor = ipcAccessor;
  private readonly ILogger<ConnectionsViewModel> _logger = logger;
  private readonly IMessenger _messenger = messenger;

  [ObservableProperty]
  private ObservableCollection<RemoteControlConnection> _activeSessions = [];
  [ObservableProperty]
  private bool _isAgentConnected;
  [ObservableProperty]
  private RemoteControlConnection? _selectedConnection;

  protected override async Task OnInitializeAsync()
  {
    await base.OnInitializeAsync();

    Disposables.AddRange(
      _messenger.Register<IpcConnectionChangedMessage>(this, HandleIpcConnectionChanged),
      _hostManager.OnSessionsChanged(this, HandleHostSessionsChanged));

    RefreshSessions();

    // Initialize current status from IPC accessor
    IsAgentConnected = _ipcAccessor.TryGetClient(out var client) && client?.IsConnected == true;
  }

  [RelayCommand]
  private async Task Disconnect()
  {
    try
    {
      if (SelectedConnection is null)
      {
        return;
      }

      var result = await _dialogProvider.ShowMessageBox(
        Localization.ConfirmDisconnectTitle,
        Localization.ConfirmDisconnect,
        MessageBoxButtons.YesNo);

      if (result == MessageBoxResult.Yes)
      {
        if (!_hostManager.TryGetSession(SelectedConnection.SessionId, out var session))
        {
          RefreshSessions();
          return;
        }
        var remoteControlStream = session.Host.Services.GetRequiredService<IDesktopRemoteControlStream>();
        await remoteControlStream.RequestDisconnect();
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error disconnecting from remote control session.");
      await _dialogProvider.ShowMessageBox(
        Localization.UnhandledExceptionTitle,
        Localization.UnhandledExceptionMessage,
        MessageBoxButtons.Ok);
      return;
    }

  }

  private Task HandleHostSessionsChanged(ICollection<RemoteControlSession> collection)
  {
    Dispatcher.UIThread.Invoke(() =>
    {
      ActiveSessions.Clear();
      var sessions = collection.Select(x =>
        new RemoteControlConnection(x.SessionId, x.RequestDto.ViewerName, x.ConnectedAt));
      ActiveSessions.AddRange(sessions);
    });
    return Task.CompletedTask;
  }

  private Task HandleIpcConnectionChanged(object subscriber, IpcConnectionChangedMessage message)
  {
    // Ensure UI update happens on UI thread
    Dispatcher.UIThread.Invoke(() =>
    {
      IsAgentConnected = message.IsConnected;
    });
    return Task.CompletedTask;
  }

  private void RefreshSessions()
  {
    Dispatcher.UIThread.Post(() =>
    {
      try
      {
        ActiveSessions.Clear();
        var sessions = _hostManager
          .GetAllSessions()
          .Select(x => new RemoteControlConnection(x.SessionId, x.RequestDto.ViewerName, x.ConnectedAt));

        ActiveSessions.AddRange(sessions);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error refreshing remote control sessions.");
      }
    });
  }
}
