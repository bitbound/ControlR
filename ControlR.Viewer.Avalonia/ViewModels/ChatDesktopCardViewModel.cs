using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlR.Viewer.Avalonia.ViewModels;

public interface IChatDesktopCardViewModel
{
  string Name { get; }
  int ProcessId { get; }
  DesktopSession Session { get; }
  IAsyncRelayCommand StartChatCommand { get; }
  int SystemSessionId { get; }
  DesktopSessionType Type { get; }
  string Username { get; }
}

public partial class ChatDesktopCardViewModel(
  DesktopSession session,
  Func<DesktopSession, Task>? startChatCallback = null) : ObservableObject, IChatDesktopCardViewModel
{
  private readonly DesktopSession _session = session;
  private readonly Func<DesktopSession, Task>? _startChatCallback = startChatCallback;

  public string Name => _session.Name;
  public int ProcessId => _session.ProcessId;
  public DesktopSession Session => _session;
  public int SystemSessionId => _session.SystemSessionId;
  public DesktopSessionType Type => _session.Type;
  public string Username => _session.Username;

  [RelayCommand]
  private async Task StartChat()
  {
    if (_startChatCallback is not null)
    {
      await _startChatCallback.Invoke(_session);
    }
  }
}
