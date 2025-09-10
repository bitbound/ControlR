using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Client.Components.Pages;

public partial class PersonalAccessTokens
{
  private PersonalAccessTokenDto[] _personalAccessTokens = [];
  private bool _isLoading = false;
  private string _newTokenName = string.Empty;
  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  protected override async Task OnInitializedAsync()
  {
    await LoadPersonalAccessTokens();
  }

  private async Task CreatePersonalAccessToken()
  {
    if (string.IsNullOrWhiteSpace(_newTokenName))
      return;

    _isLoading = true;
    try
    {
      var request = new CreatePersonalAccessTokenRequestDto(_newTokenName.Trim());
      var result = await ControlrApi.CreatePersonalAccessToken(request);

      if (result.IsSuccess)
      {
        // Show the dialog with the new personal access token
        var parameters = new DialogParameters
        {
          { nameof(PersonalAccessTokenDialog.PersonalAccessToken), result.Value.PersonalAccessToken },
          { nameof(PersonalAccessTokenDialog.PlainTextKey), result.Value.PlainTextToken }
        };

        var dialogOptions = new DialogOptions
        {
          BackdropClick = false,
          FullWidth = true,
          MaxWidth = MaxWidth.Small
        };

        await DialogService.ShowAsync<PersonalAccessTokenDialog>("Personal Access Token Created", parameters, dialogOptions);

        // Refresh the list and clear the input
        await LoadPersonalAccessTokens();
        _newTokenName = string.Empty;
        Snackbar.Add("Personal access token created successfully", Severity.Success);
      }
      else
      {
        Snackbar.Add($"Failed to create personal access token: {result.Reason}", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Error creating personal access token: {ex.Message}", Severity.Error);
    }
    finally
    {
      _isLoading = false;
    }
  }

  private async Task DeletePersonalAccessToken(PersonalAccessTokenDto personalAccessToken)
  {
    var confirmed = await DialogService.ShowMessageBox(
      "Confirm Delete",
      $"Are you sure you want to delete the personal access token '{personalAccessToken.Name}'?",
      yesText: "Delete",
      cancelText: "Cancel");

    if (confirmed == true)
    {
      try
      {
        var result = await ControlrApi.DeletePersonalAccessToken(personalAccessToken.Id);
        if (result.IsSuccess)
        {
          await LoadPersonalAccessTokens();
          Snackbar.Add("Personal access token deleted successfully", Severity.Success);
        }
        else
        {
          Snackbar.Add($"Failed to delete personal access token: {result.Reason}", Severity.Error);
        }
      }
      catch (Exception ex)
      {
        Snackbar.Add($"Error deleting personal access token: {ex.Message}", Severity.Error);
      }
    }
  }

  private async Task LoadPersonalAccessTokens()
  {
    _isLoading = true;
    try
    {
      var result = await ControlrApi.GetPersonalAccessTokens();
      if (result.IsSuccess)
      {
        _personalAccessTokens = result.Value;
      }
      else
      {
        Snackbar.Add($"Failed to load personal access tokens: {result.Reason}", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Error loading personal access tokens: {ex.Message}", Severity.Error);
    }
    finally
    {
      _isLoading = false;
    }
  }
  private async Task OnKeyDown(KeyboardEventArgs e)
  {
    if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_newTokenName))
    {
      await CreatePersonalAccessToken();
    }
  }

  private async Task RenamePersonalAccessToken(PersonalAccessTokenDto personalAccessToken)
  {
    var parameters = new DialogParameters
    {
      { "CurrentName", personalAccessToken.Name }
    };
    var dialogOptions = new DialogOptions
    {
      CloseButton = true,
      FullWidth = true,
      MaxWidth = MaxWidth.ExtraSmall
    };
    var newTokenName = await DialogService.ShowPrompt(
      title: "Rename Personal Access Token", 
      subtitle: $"Rename the '{personalAccessToken.Name}' token by providing a new name.",
      inputLabel: "New Name",
      inputHintText: "Enter a new name for the personal access token.");

    if (string.IsNullOrWhiteSpace(newTokenName))
    {
      return;
    }

    try
    {
      var updateRequest = new UpdatePersonalAccessTokenRequestDto(newTokenName.Trim());
      var updateResult = await ControlrApi.UpdatePersonalAccessToken(personalAccessToken.Id, updateRequest);
      if (updateResult.IsSuccess)
      {
        await LoadPersonalAccessTokens();
        Snackbar.Add("Personal access token renamed successfully", Severity.Success);
      }
      else
      {
        Snackbar.Add($"Failed to rename personal access token: {updateResult.Reason}", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add($"Error renaming personal access token: {ex.Message}", Severity.Error);
    }
  }
}
