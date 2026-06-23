using System.Collections.ObjectModel;
using ControlR.Libraries.Avalonia.Controls.Snackbar;

namespace ControlR.Viewer.Avalonia.Tests.Fakes;

public sealed class FakeSnackbar : ISnackbar
{
  private readonly ObservableCollection<SnackbarMessage> _messages = [];

  public FakeSnackbar()
  {
    Messages = new ReadOnlyObservableCollection<SnackbarMessage>(_messages);
  }

  public List<SnackbarCall> Calls { get; } = [];
  public ReadOnlyObservableCollection<SnackbarMessage> Messages { get; }
  public SnackbarOptions Options { get; } = new();

  public Guid Add(
    string message,
    SnackbarSeverity severity = SnackbarSeverity.Info,
    Action<SnackbarMessageOptions>? configure = null)
  {
    var id = Guid.NewGuid();
    Calls.Add(new SnackbarCall(message, severity, configure));

    var snackbarMessage = new SnackbarMessage(id, message, severity);
    _messages.Add(snackbarMessage);

    return id;
  }

  public void Clear()
  {
    _messages.Clear();
  }

  public bool Remove(Guid id)
  {
    var message = _messages.FirstOrDefault(x => x.Id == id);
    if (message is null)
    {
      return false;
    }

    _messages.Remove(message);
    return true;
  }
}

public sealed record SnackbarCall(
  string Message,
  SnackbarSeverity Severity,
  Action<SnackbarMessageOptions>? Configure);
