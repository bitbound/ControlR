using System.Collections.ObjectModel;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.Libraries.Avalonia.Controls.Snackbar;

public interface ISnackbar
{
  ReadOnlyObservableCollection<SnackbarMessage> Messages { get; }
  SnackbarOptions Options { get; }

  Guid Add(
    string message,
    SnackbarSeverity severity = SnackbarSeverity.Info,
    Action<SnackbarMessageOptions>? configure = null);
  void Clear();
  bool Remove(Guid id);
}

internal sealed class SnackbarService : ISnackbar
{
  private readonly Dictionary<Guid, CancellationTokenSource> _dismissCancellations = [];
  private readonly ILogger<SnackbarService> _logger;
  private readonly ObservableCollection<SnackbarMessage> _messages = [];
  private readonly SnackbarOptions _options;
  private readonly Lock _sync = new();
  
  public SnackbarService(ILogger<SnackbarService> logger, IOptions<SnackbarOptions> options)
  {
    _logger = logger;
    _options = options.Value;
    Messages = new ReadOnlyObservableCollection<SnackbarMessage>(_messages);
  }

  public ReadOnlyObservableCollection<SnackbarMessage> Messages { get; }
  public SnackbarOptions Options => _options;

  public Guid Add(
    string message,
    SnackbarSeverity severity = SnackbarSeverity.Info,
    Action<SnackbarMessageOptions>? configure = null)
  {
    if (string.IsNullOrWhiteSpace(message))
    {
      throw new ArgumentException("Message cannot be null or whitespace.", nameof(message));
    }

    var messageOptions = new SnackbarMessageOptions();
    configure?.Invoke(messageOptions);

    var id = Guid.NewGuid();
    var snackbarMessage = new SnackbarMessage(id, message, severity)
    {
      Opacity = 0,
    };

    using (_sync.EnterScope())
    {
      var dismissCancellation = new CancellationTokenSource();
      _dismissCancellations[id] = dismissCancellation;
      AddMessage(snackbarMessage);
      _ = RunAutoDismiss(
        id,
        messageOptions.VisibleStateDuration ?? _options.VisibleStateDuration,
        dismissCancellation.Token);
    }

    _ = Dispatcher.UIThread.InvokeAsync(() => snackbarMessage.Opacity = 1);
    return id;
  }

  public void Clear()
  {
    using (_sync.EnterScope())
    {
      foreach (var (_, cancellationTokenSource) in _dismissCancellations)
      {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
      }

      _dismissCancellations.Clear();
      _ = Dispatcher.UIThread.InvokeAsync(_messages.Clear);
    }
  }

  public bool Remove(Guid id)
  {
    using (_sync.EnterScope())
    {
      if (!_dismissCancellations.Remove(id, out var cancellationTokenSource))
      {
        return false;
      }

      cancellationTokenSource.Cancel();
      cancellationTokenSource.Dispose();

      RemoveMessageById(id);
      return true;
    }
  }

  private void AddMessage(SnackbarMessage message)
  {
    void AddToCollection()
    {
      if (_options.NewestOnTop)
      {
        _messages.Insert(0, message);
      }
      else
      {
        _messages.Add(message);
      }
    }

    if (Dispatcher.UIThread.CheckAccess())
    {
      AddToCollection();
      return;
    }

    Dispatcher.UIThread.Post(AddToCollection);
  }

  private void RemoveMessageById(Guid id)
  {
    void Remove()
    {
      var message = _messages.FirstOrDefault(x => x.Id == id);
      if (message is null)
      {
        return;
      }

      _messages.Remove(message);
    }

    if (Dispatcher.UIThread.CheckAccess())
    {
      Remove();
      return;
    }

    Dispatcher.UIThread.Post(Remove);
  }

  private async Task RunAutoDismiss(Guid id, TimeSpan visibleStateDuration, CancellationToken cancellationToken)
  {
    try
    {
      if (visibleStateDuration > TimeSpan.Zero)
      {
        await Task.Delay(visibleStateDuration, cancellationToken);
      }

      var message = await Dispatcher.UIThread.InvokeAsync(() => _messages.FirstOrDefault(x => x.Id == id));

      if (message is null)
      {
        return;
      }

      await Dispatcher.UIThread.InvokeAsync(() => message.Opacity = 0);

      if (_options.FadeDuration > TimeSpan.Zero)
      {
        await Task.Delay(_options.FadeDuration, cancellationToken);
      }

      using (_sync.EnterScope())
      {
        if (_dismissCancellations.Remove(id, out var cancellationTokenSource))
        {
          cancellationTokenSource.Dispose();
        }
      }

      RemoveMessageById(id);
    }
    catch (OperationCanceledException)
    {
      // Expected when a message is removed manually before auto-dismissal.
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during auto-dismissal of snackbar message with ID {MessageId}.", id);
    }
  }
}