using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Pages;

public partial class InstallerKeys
{
  private IEnumerable<AgentInstallerKeyDto> _keys = [];
  private bool _loading = true;
  private string _searchString = "";

  [Inject]
  public required IControlrApi ControlrApi { get; init; }
  [Inject]
  public required IDialogService DialogService { get; init; }
  [Inject]
  public required ILogger<InstallerKeys> Logger { get; init; }
  [Inject]
  public required ISnackbar Snackbar { get; init; }


  private Func<AgentInstallerKeyDto, bool> _quickFilter => key =>
  {
    if (string.IsNullOrWhiteSpace(_searchString))
    {
      return true;
    }

    var dtoJson = JsonSerializer.Serialize(key);
    if (dtoJson.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    return false;
  };

  protected override async Task OnInitializedAsync()
  {
    try
    {
      await base.OnInitializedAsync();
      await LoadKeys();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Unexpected error during OnInitializedAsync for InstallerKeys.");
      Snackbar.Add("An error occurred initializing the installer keys page.", Severity.Error);
      _loading = false;
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task DeleteKey(AgentInstallerKeyDto key)
  {
    try
    {
    var confirmed = await DialogService.ShowMessageBox(
        "Confirm Delete",
        $"Are you sure you want to delete the key \"{key.FriendlyName ?? key.Id.ToString()}\"?",
        yesText: "Delete",
        cancelText: "Cancel");

    if (confirmed != true)
    {
      return;
    }
    var apiResult = await ControlrApi.DeleteInstallerKey(key.Id);
    if (apiResult.IsSuccess)
    {
      Snackbar.Add("Key deleted.", Severity.Success);
      await LoadKeys();
    }
    else
    {
      Snackbar.Add($"Failed to delete key: {apiResult.Reason}", Severity.Error);
    }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error deleting installer key {KeyId}.", key?.Id);
      Snackbar.Add("An error occurred while deleting the key.", Severity.Error);
    }
  }
  private async Task<bool> LoadKeys()
  {
    try
    {
      _loading = true;
      await InvokeAsync(StateHasChanged);

      var result = await ControlrApi.GetAllInstallerKeys();
      if (result.IsSuccess)
      {
        _keys = result.Value;
        return true;
      }
      else
      {
        Snackbar.Add("Failed to load installer keys.", Severity.Error);
        return false;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error loading installer keys.");
      return false;
    }
    finally
    {
      _loading = false;
      await InvokeAsync(StateHasChanged);
    }
  }
  private async Task RefreshKeysClicked()
  {
    try
    {
      var refreshed = await LoadKeys();
      if (refreshed)
      {
        Snackbar.Add("Installer keys refreshed", Severity.Success);
      }
      else
      {
        Snackbar.Add("Failed to refresh installer keys.", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error refreshing installer keys.");
      Snackbar.Add("Failed to refresh installer keys.", Severity.Error);
    }
  }
  private async Task RenameKey(AgentInstallerKeyDto key)
  {
    try
    {
    var newName = await DialogService.ShowPrompt(
        title: "Rename Key",
        subtitle: $"Enter a new name for the key \"{key.FriendlyName ?? key.Id.ToString()}\".",
        inputLabel: "New Name",
        inputHintText: "Enter a new name here.");

    if (string.IsNullOrWhiteSpace(newName))
    {
      return;
    }

    var dto = new RenameInstallerKeyRequestDto(key.Id, newName);
    var result = await ControlrApi.RenameInstallerKey(dto);

    if (result.IsSuccess)
    {
      Snackbar.Add("Key renamed.", Severity.Success);
      await LoadKeys();
    }
    else
    {
      Snackbar.Add($"Failed to rename key: {result.Reason}", Severity.Error);
    }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error renaming installer key {KeyId}.", key?.Id);
      Snackbar.Add("An error occurred while renaming the key.", Severity.Error);
    }
  }
  private async Task ShowUsages(AgentInstallerKeyDto key)
  {
    try
    {
    var parameters = new DialogParameters
        {
            { "Usages", key.Usages }
        };

    var options = new DialogOptions
    {
      CloseButton = true,
      MaxWidth = MaxWidth.Medium,
      FullWidth = true
    };

    await DialogService.ShowAsync<InstallerKeyUsagesDialog>("Key Usages", parameters, options);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error showing usages for installer key {KeyId}.", key?.Id);
      Snackbar.Add("An error occurred while showing key usages.", Severity.Error);
    }
  }
}
