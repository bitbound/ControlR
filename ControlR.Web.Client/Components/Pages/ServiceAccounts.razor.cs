using ControlR.Web.Client.Components.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using SATos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServiceAccounts;

namespace ControlR.Web.Client.Components.Pages;

public partial class ServiceAccounts : ComponentBase
{
  private readonly HashSet<Guid> _togglingIds = [];

  private SATos.TenantServiceAccountDto[] _accounts = [];
  private bool _canRotateCredentials;
  private bool _loading;
  private string _searchString = string.Empty;
  private Guid _tenantId;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Inject]
  public required IClipboardManager ClipboardManager { get; init; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required TimeProvider TimeProvider { get; init; }

  private Func<SATos.TenantServiceAccountDto, bool> QuickFilter => account =>
  {
    if (string.IsNullOrWhiteSpace(_searchString))
    {
      return true;
    }

    return account.Name.Contains(_searchString, StringComparison.OrdinalIgnoreCase) ||
           (account.Description?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) ?? false);
  };

  protected override async Task OnInitializedAsync()
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    if (!state.User.TryGetTenantId(out var tenantId))
    {
      Snackbar.Add("No tenant is associated with the signed-in user.", Severity.Error);
      return;
    }

    _tenantId = tenantId;
    _canRotateCredentials = state.User.HasClientPolicy(PolicyNames.RequireServiceAccountRotateCredentials);
    await Refresh();
  }

  private static string TruncateId(Guid id)
  {
    return $"{id.ToString()[..8]}...";
  }

  private async Task AddCredential(SATos.TenantServiceAccountDto account)
  {
    var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Small };
    var dialog = await DialogService.ShowAsync<CreateServiceAccountCredentialDialog>(
      $"Add Credential to \"{account.Name}\"", options);
    var result = await dialog.Result;

    if (result is null || result.Canceled || result.Data is not CreateServiceAccountCredentialDialogResult dialogResult)
    {
      return;
    }

    var apiResult = await ControlrApi.V1.TenantServiceAccounts.AddCredential(
      _tenantId, account.Id, new SATos.CreateServiceAccountCredentialRequestDto(dialogResult.Name, dialogResult.ExpiresAt));

    if (!apiResult.IsSuccess)
    {
      Snackbar.Add(apiResult.Reason, Severity.Error);
      return;
    }

    await ShowSecretDialog("Credential Created", apiResult.Value.PlainTextSecretKey, apiResult.Value.Credential.Name);
    await Refresh();
  }

  private async Task CopyId(Guid id)
  {
    await ClipboardManager.SetText(id.ToString());
    Snackbar.Add("Copied to clipboard", Severity.Success);
  }

  private async Task CreateAccount()
  {
    var canIssueCredential = await HasPolicy(PolicyNames.RequireServiceAccountRotateCredentials);

    var parameters = new DialogParameters<CreateServiceAccountDialog>
    {
      { x => x.CanIssueCredential, canIssueCredential },
      { x => x.CanGrantUnrestricted, false },
      { x => x.RotatePermissionLabel, "Rotate Service Account Credentials" }
    };

    var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Small };
    var dialog = await DialogService.ShowAsync<CreateServiceAccountDialog>("Create Service Account", parameters, options);
    var result = await dialog.Result;

    if (result is null || result.Canceled || result.Data is not CreateServiceAccountDialogResult dialogResult)
    {
      return;
    }

    var createResult = await ControlrApi.V1.TenantServiceAccounts.Create(
      _tenantId, new SATos.CreateServiceAccountRequestDto(dialogResult.Name, dialogResult.Description));

    if (!createResult.IsSuccess)
    {
      Snackbar.Add(createResult.Reason, Severity.Error);
      return;
    }

    var account = createResult.Value;

    if (dialogResult.CredentialName is { } credentialName)
    {
      var credResult = await ControlrApi.V1.TenantServiceAccounts.AddCredential(
        _tenantId, account.Id, new SATos.CreateServiceAccountCredentialRequestDto(credentialName, dialogResult.CredentialExpiresAt));

      if (!credResult.IsSuccess)
      {
        Snackbar.Add($"Account created, but credential creation failed: {credResult.Reason}", Severity.Warning);
        await Refresh();
        return;
      }

      await ShowSecretDialog("Service Account Created", credResult.Value.PlainTextSecretKey, account.Name);
    }
    else
    {
      Snackbar.Add("Service account created without a credential", Severity.Success);
    }

    await EditPermissions(account);
    await Refresh();
  }

  private async Task DeleteAccount(SATos.TenantServiceAccountDto account)
  {
    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Delete Service Account",
      $"Are you sure you want to delete \"{account.Name}\"? All credentials will be revoked.",
      "Delete", "Cancel");

    if (!confirmed.GetValueOrDefault())
    {
      return;
    }

    var result = await ControlrApi.V1.TenantServiceAccounts.Delete(_tenantId, account.Id);
    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Service account deleted", Severity.Success);
    await Refresh();
  }

  private async Task EditAccount(SATos.TenantServiceAccountDto account)
  {
    var parameters = new DialogParameters<EditServiceAccountDialog>
    {
      { x => x.Name, account.Name },
      { x => x.Description, account.Description }
    };

    var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Small };
    var dialog = await DialogService.ShowAsync<EditServiceAccountDialog>($"Edit {account.Name}", parameters, options);
    var result = await dialog.Result;

    if (result is null || result.Canceled || result.Data is not EditServiceAccountDialogResult editResult)
    {
      return;
    }

    var index = Array.FindIndex(_accounts, x => x.Id == account.Id);
    var currentEnabled = index >= 0 ? _accounts[index].IsEnabled : account.IsEnabled;

    var updateResult = await ControlrApi.V1.TenantServiceAccounts.Update(
      _tenantId, account.Id, new SATos.UpdateServiceAccountRequestDto(editResult.Name, editResult.Description, currentEnabled));

    if (!updateResult.IsSuccess)
    {
      Snackbar.Add(updateResult.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Service account updated", Severity.Success);
    await Refresh();
  }

  private async Task EditPermissions(SATos.TenantServiceAccountDto account)
  {
    var parameters = new DialogParameters<PermissionAssignmentPanelDialog>
    {
      { x => x.PrincipalKind, PermissionPrincipalKind.ServiceAccount },
      { x => x.PrincipalId, account.Id }
    };

    var dialog = await DialogService.ShowAsync<PermissionAssignmentPanelDialog>(
      $"Permissions: {account.Name}", parameters, PermissionAssignmentPanelDialog.DefaultOptions);
    await dialog.Result;
    await Refresh();
  }

  private int GetActiveCount(IReadOnlyList<SATos.ServiceAccountCredentialDto> credentials)
  {
    return credentials.Count(cred =>
      cred.RevokedAt is null && (cred.ExpiresAt is null || cred.ExpiresAt > TimeProvider.GetUtcNow()));
  }

  private async Task<bool> HasPolicy(string policyName)
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    return state.User.HasClientPolicy(policyName);
  }

  private async Task PurgeCredential(Guid serviceAccountId, Guid credentialId)
  {
    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Delete Credential",
      "Are you sure you want to permanently delete this credential? This cannot be undone.",
      "Delete", "Cancel");

    if (!confirmed.GetValueOrDefault())
    {
      return;
    }

    var result = await ControlrApi.V1.TenantServiceAccounts.PurgeCredential(_tenantId, serviceAccountId, credentialId);
    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Credential deleted", Severity.Success);
    await Refresh();
  }

  private async Task Refresh()
  {
    _loading = true;
    StateHasChanged();

    try
    {
      var result = await ControlrApi.V1.TenantServiceAccounts.GetAll(_tenantId);
      if (result.IsSuccess)
      {
        _accounts = [.. result.Value.Items];
      }
      else
      {
        Snackbar.Add(result.Reason, Severity.Error);
      }
    }
    finally
    {
      _loading = false;
      StateHasChanged();
    }
  }

  private async Task RevokeCredential(Guid serviceAccountId, Guid credentialId)
  {
    var confirmed = await DialogService.ShowMessageBoxAsync(
      "Revoke Credential",
      "Are you sure you want to revoke this credential? The holder will no longer be able to authenticate.",
      "Revoke", "Cancel");

    if (!confirmed.GetValueOrDefault())
    {
      return;
    }

    var result = await ControlrApi.V1.TenantServiceAccounts.RevokeCredential(_tenantId, serviceAccountId, credentialId);
    if (!result.IsSuccess)
    {
      Snackbar.Add(result.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Credential revoked", Severity.Success);
    await Refresh();
  }

  private async Task ShowSecretDialog(string title, string secret, string subtitle)
  {
    var parameters = new DialogParameters<SecretDisplayDialog>
    {
      { x => x.Title, title },
      { x => x.Secret, secret },
      { x => x.SecretLabel, "Secret Key" },
      { x => x.Subtitle, subtitle },
      { x => x.SubtitleLabel, "Name" }
    };

    var options = SecretDisplayDialog.DefaultOptions;

    var dialogRef = await DialogService.ShowAsync<SecretDisplayDialog>(title, parameters, options);
    await dialogRef.Result;
  }

  private async Task ToggleEnabled(SATos.TenantServiceAccountDto account, bool enabled)
  {
    if (_togglingIds.Contains(account.Id)) return;

    _togglingIds.Add(account.Id);
    try
    {
      var index = Array.FindIndex(_accounts, x => x.Id == account.Id);
      if (index < 0) return;

      var latest = _accounts[index];
      var result = await ControlrApi.V1.TenantServiceAccounts.Update(_tenantId, latest.Id,
        new SATos.UpdateServiceAccountRequestDto(latest.Name, latest.Description, enabled));

      if (!result.IsSuccess)
      {
        Snackbar.Add(result.Reason, Severity.Error);
        return;
      }

      index = Array.FindIndex(_accounts, x => x.Id == account.Id);
      if (index >= 0)
      {
        _accounts = [.. _accounts[..index], result.Value, .. _accounts[(index + 1)..]];
      }
    }
    finally
    {
      _togglingIds.Remove(account.Id);
    }
  }
}
