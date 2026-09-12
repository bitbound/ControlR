using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;
namespace ControlR.Web.Client.Components.Pages;

public partial class InstallerKeys
{
  private IEnumerable<InstallerKeyDto> _keys = [];
  private bool _loading = true;
  private string _searchString = "";
  private Guid _tenantId;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required ILogger<InstallerKeys> Logger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private Func<InstallerKeyDto, bool> QuickFilter => key =>
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
      if (await AuthState.GetTenantIdAsync(Snackbar) is not { } tenantId)
      {
        _loading = false;
        return;
      }

      _tenantId = tenantId;
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

  private async Task DeleteKey(InstallerKeyDto key)
  {
    try
    {
      var confirmed = await DialogService.ShowMessageBoxAsync(
          "Confirm Delete",
          $"Are you sure you want to delete the key \"{key.FriendlyName ?? key.Id.ToString()}\"?",
          yesText: "Delete",
          cancelText: "Cancel");

      if (confirmed != true)
      {
        return;
      }
      var apiResult = await ControlrApi.V1.InstallerKeys.DeleteInstallerKey(key.Id, _tenantId);
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

      var result = await ControlrApi.V1.InstallerKeys.GetAllInstallerKeys(_tenantId);
      if (result.IsSuccess)
      {
        _keys = result.Value.Items;
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

  private async Task RenameKey(InstallerKeyDto key)
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

      var result = await ControlrApi.V1.InstallerKeys.RenameInstallerKey(
          key.Id, _tenantId, new RenameInstallerKeyRequestDto(newName));

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

  private async Task ShowUsages(InstallerKeyDto key)
  {
    try
    {
      var result = await ControlrApi.V1.InstallerKeys.GetInstallerKeyUsages(key.Id, _tenantId);
      if (!result.IsSuccess)
      {
        Snackbar.Add($"Failed to load key usages: {result.Reason}", Severity.Error);
        return;
      }

      var parameters = new DialogParameters
        {
            { "Usages", result.Value.Items }
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
