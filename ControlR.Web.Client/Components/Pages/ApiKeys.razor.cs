using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Client.Components.Dialogs;
using ControlR.Web.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace ControlR.Web.Client.Components.Pages;

public partial class ApiKeys
{
  private List<ApiKeyDto> _apiKeys = [];
  private string _newKeyName = string.Empty;
  private bool _isLoading = false;

  [Inject]
  private IControlrApi ControlrApi { get; set; } = default!;

  [Inject]
  private IDialogService DialogService { get; set; } = default!;

  [Inject]
  private ISnackbar Snackbar { get; set; } = default!;

  protected override async Task OnInitializedAsync()
  {
    await LoadApiKeys();
  }

  private async Task LoadApiKeys()
  {
    _isLoading = true;
    try
    {
      var result = await ControlrApi.GetApiKeys();
      if (result.IsSuccess)
      {
        _apiKeys = result.Value.ToList();
      }
      else
      {
        Snackbar.Add($"Failed to load API keys: {result.Reason}", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Error loading API keys: {ex.Message}", Severity.Error);
    }
    finally
    {
      _isLoading = false;
    }
  }

  private async Task CreateApiKey()
  {
    if (string.IsNullOrWhiteSpace(_newKeyName))
      return;

    _isLoading = true;
    try
    {
      var request = new CreateApiKeyRequestDto(_newKeyName.Trim());
      var result = await ControlrApi.CreateApiKey(request);
      
      if (result.IsSuccess)
      {
        // Show the dialog with the new API key
        var parameters = new DialogParameters
        {
          { nameof(ApiKeyDialog.ApiKey), result.Value.ApiKey },
          { nameof(ApiKeyDialog.PlainTextKey), result.Value.PlainTextKey }
        };

        var dialogOptions = new DialogOptions
        {
          BackdropClick = false,
          FullWidth = true,
          MaxWidth = MaxWidth.Small
        };

        await DialogService.ShowAsync<ApiKeyDialog>("API Key Created", parameters, dialogOptions);

        // Refresh the list and clear the input
        await LoadApiKeys();
        _newKeyName = string.Empty;
        Snackbar.Add("API key created successfully", Severity.Success);
      }
      else
      {
        Snackbar.Add($"Failed to create API key: {result.Reason}", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Error creating API key: {ex.Message}", Severity.Error);
    }
    finally
    {
      _isLoading = false;
    }
  }

  private async Task DeleteApiKey(ApiKeyDto apiKey)
  {
    var confirmed = await DialogService.ShowMessageBox(
      "Confirm Delete",
      $"Are you sure you want to delete the API key '{apiKey.FriendlyName}'?",
      yesText: "Delete",
      cancelText: "Cancel");

    if (confirmed == true)
    {
      try
      {
        var result = await ControlrApi.DeleteApiKey(apiKey.Id);
        if (result.IsSuccess)
        {
          await LoadApiKeys();
          Snackbar.Add("API key deleted successfully", Severity.Success);
        }
        else
        {
          Snackbar.Add($"Failed to delete API key: {result.Reason}", Severity.Error);
        }
      }
      catch (Exception ex)
      {
        Snackbar.Add($"Error deleting API key: {ex.Message}", Severity.Error);
      }
    }
  }

  private async Task OnKeyDown(KeyboardEventArgs e)
  {
    if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_newKeyName))
    {
      await CreateApiKey();
    }
  }
}
