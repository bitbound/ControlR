using System.Reflection;

namespace ControlR.Web.Client.Services;

public interface IWhatsNewNotifier
{
  Task CheckAndShowIfNeeded(CancellationToken cancellationToken);
  Task ShowWhatsNew(CancellationToken cancellationToken);
}

public class WhatsNewNotifier(
    IUserStorageClient userStorage,
    ISnackbar snackbar,
    IDialogService dialogService,
    ILogger<WhatsNewNotifier> logger) : IWhatsNewNotifier
{
  private readonly IDialogService _dialogService = dialogService;
  private readonly ILogger<WhatsNewNotifier> _logger = logger;
  private readonly ISnackbar _snackbar = snackbar;
  private readonly IUserStorageClient _userStorage = userStorage;

  private bool _checkInProgress;

  public async Task CheckAndShowIfNeeded(CancellationToken cancellationToken)
  {
    if (_checkInProgress)
      return;

    _checkInProgress = true;
    try
    {
      var currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

      if (string.IsNullOrWhiteSpace(currentVersion))
      {
        _logger.LogError("Failed to get assembly version.");
        _snackbar.Add("Failed to get assembly version.", Severity.Error);
        return;
      }

      var acknowledged = await _userStorage.GetItem(UserStorageKeys.AcknowledgedNewVersion, cancellationToken);
      if (acknowledged == currentVersion)
        return;

      await ShowWhatsNew(currentVersion, cancellationToken);
    }
    finally
    {
      _checkInProgress = false;
    }
  }

  public async Task ShowWhatsNew(CancellationToken cancellationToken)
  {
    var currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
    if (string.IsNullOrWhiteSpace(currentVersion))
    {
      _logger.LogError("Failed to get assembly version.");
      _snackbar.Add("Failed to get assembly version.", Severity.Error);
      return;
    }

    await ShowWhatsNew(currentVersion, cancellationToken);
  }

  private async Task ShowWhatsNew(string currentVersion, CancellationToken cancellationToken)
  {
    var options = new DialogOptions
    {
      CloseOnEscapeKey = true,
      MaxWidth = MaxWidth.Medium,
      FullWidth = true
    };

    var dialogParams = new DialogParameters
    {
      [nameof(WhatsNewDialog.CurrentVersion)] = currentVersion
    };

    var dialogRef = await _dialogService.ShowAsync<WhatsNewDialog>(
      title: "What's New",
      options: options,
      parameters: dialogParams);

    var result = await dialogRef.Result;

    if (result is { Canceled: false, Data: true })
    {
      await _userStorage.SetItem(UserStorageKeys.AcknowledgedNewVersion, currentVersion, cancellationToken);
    }
  }
}
