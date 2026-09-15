using Microsoft.AspNetCore.Components.Authorization;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserGroups;

namespace ControlR.Web.Client.Components.Shared;

public sealed record PrincipalOption(Guid Id, string DisplayName, PermissionPrincipalKind Kind);

public partial class PrincipalAutocomplete
{
  private PrincipalOption? _selected;

  [Parameter]
  public ServiceAccountKind AccountKind { get; set; } = ServiceAccountKind.Tenant;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

  [Parameter]
  public string? Class { get; set; }

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Parameter]
  public bool Disabled { get; set; }

  [Parameter]
  public string Label { get; set; } = "Principal";

  [Parameter]
  public PermissionPrincipalKind PrincipalKind { get; set; } = PermissionPrincipalKind.User;

  [Parameter]
  public Guid? SelectedId { get; set; }

  [Parameter]
  public EventCallback<Guid?> SelectedIdChanged { get; set; }

  protected override async Task OnParametersSetAsync()
  {
    if (SelectedId is { } id && _selected?.Id != id)
    {
      _selected = await ResolveSelectedAsync(id);
    }
    else if (SelectedId is null)
    {
      _selected = null;
    }

    await base.OnParametersSetAsync();
  }

  private static string FormatPatDisplayName(PersonalAccessTokenResponseDto token)
  {
    var lastUsed = token.LastUsed is { } used ? used.ToLocalTime().ToString("d") : "Never";
    return $"{token.Name}  (Last Used: {lastUsed}  |  Token ID: {token.Id.ToString()[..8]}...)";
  }

  private static string FormatServiceAccountDisplayName(
    string name,
    bool isEnabled,
    Guid id,
    ServiceAccountKind accountKind)
  {
    var enabled = isEnabled ? "Yes" : "No";
    var kind = accountKind == ServiceAccountKind.Server ? "Server" : "Tenant";
    return $"{name}  ({kind}  |  Enabled: {enabled}  |  Account ID: {id.ToString()[..8]}...)";
  }

  private static string FormatUserGroupDisplayName(UserGroupDto group) =>
    $"{group.Name}  (Members: {group.MemberCount}  |  Group ID: {group.Id.ToString()[..8]}...)";

  private static bool Matches(string? value, string query) =>
    string.IsNullOrWhiteSpace(query) || (value?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

  private async Task<Guid?> GetTenantId()
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    return state.User.TryGetTenantId(out var tenantId) ? tenantId : null;
  }

  private async Task HandleValueChanged(PrincipalOption? value)
  {
    _selected = value;
    SelectedId = value?.Id;
    await SelectedIdChanged.InvokeAsync(SelectedId);
  }

  private async Task<PrincipalOption?> ResolvePersonalAccessToken(Guid id)
  {
    if (await GetTenantId() is not { } tenantId)
    {
      return null;
    }

    var result = await ControlrApi.V1.PersonalAccessTokens.GetPersonalAccessTokens(tenantId);
    if (!result.IsSuccess)
    {
      return null;
    }

    var match = result.Value.Items.FirstOrDefault(x => x.Id == id);
    return match is null ? null : new PrincipalOption(match.Id, FormatPatDisplayName(match), PermissionPrincipalKind.PersonalAccessToken);
  }

  private async Task<PrincipalOption?> ResolveSelectedAsync(Guid id)
  {
    return PrincipalKind switch
    {
      PermissionPrincipalKind.User => await ResolveUser(id),
      PermissionPrincipalKind.UserGroup => await ResolveUserGroup(id),
      PermissionPrincipalKind.ServiceAccount => await ResolveServiceAccount(id),
      PermissionPrincipalKind.PersonalAccessToken => await ResolvePersonalAccessToken(id),
      _ => null
    };
  }

  private async Task<PrincipalOption?> ResolveServiceAccount(Guid id)
  {
    if (AccountKind == ServiceAccountKind.Server)
    {
      var serverResult = await ControlrApi.V1.ServerServiceAccounts.GetAll();
      if (!serverResult.IsSuccess)
      {
        return null;
      }

      var serverMatch = serverResult.Value.Items.FirstOrDefault(x => x.Id == id);
      return serverMatch is null
        ? null
        : new PrincipalOption(
          serverMatch.Id,
          FormatServiceAccountDisplayName(
            serverMatch.Name, serverMatch.IsEnabled, serverMatch.Id, ServiceAccountKind.Server),
          PermissionPrincipalKind.ServiceAccount);
    }

    if (await GetTenantId() is not { } tenantId)
    {
      return null;
    }

    var tenantResult = await ControlrApi.V1.TenantServiceAccounts.GetAll(tenantId);
    if (!tenantResult.IsSuccess)
    {
      return null;
    }

    var tenantMatch = tenantResult.Value.Items.FirstOrDefault(x => x.Id == id);
    return tenantMatch is null
      ? null
      : new PrincipalOption(
        tenantMatch.Id,
        FormatServiceAccountDisplayName(
          tenantMatch.Name, tenantMatch.IsEnabled, tenantMatch.Id, ServiceAccountKind.Tenant),
        PermissionPrincipalKind.ServiceAccount);
  }

  private async Task<PrincipalOption?> ResolveUser(Guid id)
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    if (!state.User.TryGetTenantId(out var tenantId))
    {
      return null;
    }

    var result = await ControlrApi.V1.Users.GetAllUsers(tenantId);
    if (!result.IsSuccess)
    {
      return null;
    }

    var match = result.Value.Items.FirstOrDefault(x => x.Id == id);
    if (match is null)
    {
      return null;
    }

    return new PrincipalOption(match.Id, UserDisplay.GetDisplayName(match), PermissionPrincipalKind.User);
  }

  private async Task<PrincipalOption?> ResolveUserGroup(Guid id)
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    if (!state.User.TryGetTenantId(out var tenantId))
    {
      return null;
    }

    var result = await ControlrApi.V1.UserGroups.GetAllUserGroups(tenantId);
    if (!result.IsSuccess)
    {
      return null;
    }

    var match = result.Value.Items.FirstOrDefault(x => x.Id == id);
    return match is null ? null : new PrincipalOption(match.Id, FormatUserGroupDisplayName(match), PermissionPrincipalKind.UserGroup);
  }

  private async Task<IEnumerable<PrincipalOption>> Search(string query, CancellationToken cancellationToken)
  {
    return PrincipalKind switch
    {
      PermissionPrincipalKind.User => await SearchUsers(query, cancellationToken),
      PermissionPrincipalKind.UserGroup => await SearchUserGroups(query, cancellationToken),
      PermissionPrincipalKind.ServiceAccount => await SearchServiceAccounts(query, cancellationToken),
      PermissionPrincipalKind.PersonalAccessToken => await SearchPersonalAccessTokens(query, cancellationToken),
      _ => []
    };
  }

  private async Task<IEnumerable<PrincipalOption>> SearchPersonalAccessTokens(string query, CancellationToken cancellationToken)
  {
    if (await GetTenantId() is not { } tenantId)
    {
      return [];
    }

    var result = await ControlrApi.V1.PersonalAccessTokens.GetPersonalAccessTokens(tenantId, cancellationToken);
    if (!result.IsSuccess)
    {
      return [];
    }

    return result.Value.Items
      .Where(x => Matches(x.Name, query))
      .Select(x => new PrincipalOption(x.Id, FormatPatDisplayName(x), PermissionPrincipalKind.PersonalAccessToken));
  }

  private async Task<IEnumerable<PrincipalOption>> SearchServiceAccounts(string query, CancellationToken cancellationToken)
  {
    if (AccountKind == ServiceAccountKind.Server)
    {
      var serverResult = await ControlrApi.V1.ServerServiceAccounts.GetAll(cancellationToken);
      if (!serverResult.IsSuccess)
      {
        return [];
      }

      return serverResult.Value.Items
        .Where(x => Matches(x.Name, query))
        .Select(x => new PrincipalOption(
          x.Id,
          FormatServiceAccountDisplayName(x.Name, x.IsEnabled, x.Id, ServiceAccountKind.Server),
          PermissionPrincipalKind.ServiceAccount));
    }

    if (await GetTenantId() is not { } tenantId)
    {
      return [];
    }

    var tenantResult = await ControlrApi.V1.TenantServiceAccounts.GetAll(tenantId, cancellationToken);
    if (!tenantResult.IsSuccess)
    {
      return [];
    }

    return tenantResult.Value.Items
      .Where(x => Matches(x.Name, query))
      .Select(x => new PrincipalOption(
        x.Id,
        FormatServiceAccountDisplayName(x.Name, x.IsEnabled, x.Id, ServiceAccountKind.Tenant),
        PermissionPrincipalKind.ServiceAccount));
  }

  private async Task<IEnumerable<PrincipalOption>> SearchUserGroups(string query, CancellationToken cancellationToken)
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    if (!state.User.TryGetTenantId(out var tenantId))
    {
      return [];
    }

    var result = await ControlrApi.V1.UserGroups.GetAllUserGroups(tenantId, cancellationToken);
    if (!result.IsSuccess)
    {
      return [];
    }

    return result.Value.Items
      .Where(x => Matches(x.Name, query))
      .Select(x => new PrincipalOption(x.Id, FormatUserGroupDisplayName(x), PermissionPrincipalKind.UserGroup));
  }

  private async Task<IEnumerable<PrincipalOption>> SearchUsers(string query, CancellationToken cancellationToken)
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    if (!state.User.TryGetTenantId(out var tenantId))
    {
      return [];
    }

    var result = await ControlrApi.V1.Users.GetAllUsers(tenantId, cancellationToken);
    if (!result.IsSuccess)
    {
      return [];
    }

return result.Value.Items
       .Where(x => Matches(x.UserName, query) ||
                   Matches(x.Email, query) ||
                   Matches(x.DisplayName, query))
       .Select(x => new PrincipalOption(x.Id, UserDisplay.GetDisplayName(x), PermissionPrincipalKind.User));
  }
}
