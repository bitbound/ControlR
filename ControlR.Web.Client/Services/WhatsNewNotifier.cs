using System.Reflection;

namespace ControlR.Web.Client.Services;

public interface IWhatsNewNotifier
{
  Task CheckAndShowIfNeeded();
  Task ShowWhatsNew();
}

public class WhatsNewNotifier(
    IUserPreferencesProvider userPreferences,
    ISnackbar snackbar,
    IDialogService dialogService,
    ILogger<WhatsNewNotifier> logger) : IWhatsNewNotifier
{
  private readonly IDialogService _dialogService = dialogService;
  private readonly ILogger<WhatsNewNotifier> _logger = logger;
  private readonly ISnackbar _snackbar = snackbar;
  private readonly IUserPreferencesProvider _userPreferences = userPreferences;

  private bool _checkInProgress;

  public async Task CheckAndShowIfNeeded()
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

      var prefs = await _userPreferences.GetPreferences();

      if (prefs.AcknowledgedNewVersion == currentVersion)
        return;

      await ShowWhatsNew(currentVersion);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while checking for What's New.");
      _snackbar.Add("Error while checking release notes.", Severity.Error);
    }
    finally
    {
      _checkInProgress = false;
    }
  }

  public async Task ShowWhatsNew()
  {
    var currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
    if (string.IsNullOrWhiteSpace(currentVersion))
    {
      _logger.LogError("Failed to get assembly version.");
      _snackbar.Add("Failed to get assembly version.", Severity.Error);
      return;
    }

    try
    {
      await ShowWhatsNew(currentVersion);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while showing What's New.");
      _snackbar.Add("Error while checking release notes.", Severity.Error);
    }
  }

  private async Task ShowWhatsNew(string currentVersion)
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
      await _userPreferences.SetPreference(UserPreferenceNames.AcknowledgedNewVersion, currentVersion);
    }
  }
}