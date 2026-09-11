using ControlR.Web.Client.Components.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using PATDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

namespace ControlR.Web.Client.Components.Pages;

public partial class PersonalAccessTokens
{
  private bool _isLoading = false;
  private PersonalAccessTokenPermissionMode _newTokenMode = PersonalAccessTokenPermissionMode.Restricted;
  private string _newTokenName = string.Empty;
  private PATDtos.PersonalAccessTokenResponseDto[] _personalAccessTokens = [];

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  private bool CanCreatePersonalAccessToken =>
    !string.IsNullOrWhiteSpace(_newTokenName) &&
    !_isLoading;

  protected override async Task OnInitializedAsync()
  {
    await LoadPersonalAccessTokens();
  }

  private async Task CreatePersonalAccessToken()
  {
    if (!CanCreatePersonalAccessToken)
      return;

    _isLoading = true;
    try
    {
      if (await GetTenantId() is not { } tenantId)
      {
        Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
        return;
      }

      var request = new PATDtos.CreatePersonalAccessTokenRequestDto(
        _newTokenName.Trim(),
        _newTokenMode);
      var result = await ControlrApi.V1.PersonalAccessTokens.CreatePersonalAccessToken(tenantId, request);

      if (result.IsSuccess)
      {
        var createdToken = result.Value.PersonalAccessToken;

        var parameters = new DialogParameters<SecretDisplayDialog>
        {
          { x => x.Title, "Personal Access Token Created" },
          { x => x.Secret, result.Value.PlainTextToken },
          { x => x.SecretLabel, "Personal Access Token" },
          { x => x.Subtitle, createdToken.Name },
          { x => x.SubtitleLabel, "Token Name" }
        };

        var dialogOptions = SecretDisplayDialog.DefaultOptions;

        var dialogRef = await DialogService.ShowAsync<SecretDisplayDialog>("Personal Access Token Created", parameters, dialogOptions);
        await dialogRef.Result;

        await LoadPersonalAccessTokens();
        var createdMode = _newTokenMode;
        _newTokenName = string.Empty;
        _newTokenMode = PersonalAccessTokenPermissionMode.Restricted;
        Snackbar.Add("Personal access token created successfully", Severity.Success);

        if (createdMode != PersonalAccessTokenPermissionMode.InheritOwner)
        {
          await ManagePermissions(createdToken);
        }
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

  private async Task DeletePersonalAccessToken(PATDtos.PersonalAccessTokenResponseDto personalAccessToken)
  {
    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Confirm Delete",
      $"Are you sure you want to delete the personal access token '{personalAccessToken.Name}'?",
      yesText: "Delete",
      cancelText: "Cancel");

    if (confirmed == true)
    {
      try
      {
        if (await GetTenantId() is not { } tenantId)
        {
          Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
          return;
        }

        var result = await ControlrApi.V1.PersonalAccessTokens.DeletePersonalAccessToken(personalAccessToken.Id, tenantId);
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

  private async Task<Guid?> GetTenantId()
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    return state.User.TryGetTenantId(out var tenantId) ? tenantId : null;
  }

  private async Task LoadPersonalAccessTokens()
  {
    _isLoading = true;
    try
    {
      if (await GetTenantId() is not { } tenantId)
      {
        Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
        return;
      }

      var result = await ControlrApi.V1.PersonalAccessTokens.GetPersonalAccessTokens(tenantId);
      if (result.IsSuccess)
      {
        _personalAccessTokens = [.. result.Value.Items];
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

  private async Task ManagePermissions(PATDtos.PersonalAccessTokenResponseDto personalAccessToken)
  {
    var parameters = new DialogParameters<PermissionAssignmentPanelDialog>
    {
      { x => x.PrincipalKind, PermissionPrincipalKind.PersonalAccessToken },
      { x => x.PrincipalId, personalAccessToken.Id }
    };

    var dialogRef = await DialogService.ShowAsync<PermissionAssignmentPanelDialog>(
      $"Permissions: {personalAccessToken.Name}",
      parameters,
      PermissionAssignmentPanelDialog.DefaultOptions);
    await dialogRef.Result;

    await LoadPersonalAccessTokens();
  }

  private async Task OnKeyDown(KeyboardEventArgs e)
  {
    if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_newTokenName))
    {
      await CreatePersonalAccessToken();
    }
  }

  private async Task Refresh()
  {
    await LoadPersonalAccessTokens();
    Snackbar.Add("Personal access tokens refreshed", Severity.Success);
  }

  private async Task RenamePersonalAccessToken(PATDtos.PersonalAccessTokenResponseDto personalAccessToken)
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
      if (await GetTenantId() is not { } tenantId)
      {
        Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
        return;
      }

      var updateRequest = new PATDtos.UpdatePersonalAccessTokenRequestDto(newTokenName.Trim());
      var updateResult = await ControlrApi.V1.PersonalAccessTokens.UpdatePersonalAccessToken(personalAccessToken.Id, tenantId, updateRequest);
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
