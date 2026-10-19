using System.Diagnostics;
using System.Security.Claims;
using ControlR.Web.Client.StateManagement;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Server.Components.Account;

// This is a server-side AuthenticationStateProvider that revalidates the security stamp for the connected user
// every 30 minutes an interactive circuit is connected. It also uses PersistentComponentState to flow the
// authentication state to the client which is then fixed for the lifetime of the WebAssembly application.
internal sealed class IdentityRevalidatingAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
  private readonly IOptionsMonitor<IdentityOptions> _identityOptions;
  private readonly ILogger<IdentityRevalidatingAuthenticationStateProvider> _logger;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IOptionsMonitor<ServerLifecycleOptions> _serverLifecycleOptions;
  private readonly PersistentComponentState _state;
  private readonly PersistingComponentStateSubscription _subscription;

  private Task<AuthenticationState>? _authenticationStateTask;
  private Task<AuthenticationState>? _projectedStateTask;

  public IdentityRevalidatingAuthenticationStateProvider(
    PersistentComponentState persistentComponentState,
    ILoggerFactory loggerFactory,
    IServiceScopeFactory serviceScopeFactory,
    IOptionsMonitor<IdentityOptions> identityOptions,
    IOptionsMonitor<ServerLifecycleOptions> serverLifecycleOptions,
    ILogger<IdentityRevalidatingAuthenticationStateProvider> logger)
    : base(loggerFactory)
  {
    _scopeFactory = serviceScopeFactory;
    _state = persistentComponentState;
    _identityOptions = identityOptions;
    _serverLifecycleOptions = serverLifecycleOptions;
    _logger = logger;

    AuthenticationStateChanged += OnAuthenticationStateChanged;
    _subscription = _state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
  }

  protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

  /// <summary>
  /// Adds the <see cref="PermissionPolicies.ClientPolicyClaimType"/> grants to the cookie principal
  /// before any component reads it.
  /// </summary>
  /// <remarks>
  /// Those claims are a server-side decision, so they exist nowhere on the cookie principal. Without
  /// this, a statically rendered page sees a signed-in principal carrying no grants, and every
  /// <c>HasClientPolicy</c> call in a server-rendered component answers false. Identity pages render
  /// this way on purpose, which is what hid the tenant and server admin nav sections on them.
  /// </remarks>
  public override Task<AuthenticationState> GetAuthenticationStateAsync()
  {
    // Memoized for the provider's lifetime, which is one request. No interactive server render mode is
    // registered, so there is no circuit for a memoized grant set to go stale inside.
    return _projectedStateTask ??= ProjectClientPoliciesAsync();
  }

  protected override void Dispose(bool disposing)
  {
    _subscription.Dispose();
    AuthenticationStateChanged -= OnAuthenticationStateChanged;
    base.Dispose(disposing);
  }

  protected override async Task<bool> ValidateAuthenticationStateAsync(
    AuthenticationState authenticationState,
    CancellationToken cancellationToken)
  {
    // Get the user manager from a new scope to ensure it fetches fresh data
    await using var scope = _scopeFactory.CreateAsyncScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    return await ValidateSecurityStampAsync(userManager, authenticationState.User);
  }

  private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
  {
    _authenticationStateTask = task;
  }

  private async Task OnPersistingAsync()
  {
    if (_authenticationStateTask is null)
    {
      throw new UnreachableException($"Authentication state not set in {nameof(OnPersistingAsync)}().");
    }

    var authenticationState = await _authenticationStateTask;
    var principal = authenticationState.User;

    if (principal.Identity?.IsAuthenticated == false)
    {
      return;
    }

    var userId = principal.FindFirst(_identityOptions.CurrentValue.ClaimsIdentity.UserIdClaimType)?.Value;
    var email = principal.FindFirst(_identityOptions.CurrentValue.ClaimsIdentity.EmailClaimType)?.Value;

    if (userId is null || email is null)
    {
      return;
    }

    await using var scope = _scopeFactory.CreateAsyncScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var user = await userManager.GetUserAsync(principal);

    var userInfo = new UserInfo
    {
      UserId = userId,
      Email = email
    };

    if (user is not null)
    {
      var claims = await userManager.GetClaimsAsync(user);
      var userClaims = claims.Select(x => new UserClaim()
      {
        Type = x.Type,
        Value = x.Value
      });

      userInfo.Claims.AddRange(userClaims);

      var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
      var principalDescriptor = new PrincipalDescriptor(
        PrincipalType.User,
        user.Id,
        user.TenantId,
        principal.FindFirst(UserClaimTypes.AuthenticationMethod)?.Value ?? string.Empty);

      var grantedPolicies = await evaluator.GetGrantedPolicies(principalDescriptor, CancellationToken.None);

      var policyClaims = grantedPolicies.Select(policyName => new UserClaim
      {
        Type = PermissionPolicies.ClientPolicyClaimType,
        Value = policyName
      });
      
      userInfo.Claims.AddRange(policyClaims);
    }
    else
    {
      _logger.LogCritical(
        "User is authenticated but not found in the database. Username: {UserName}",
        principal.Identity?.Name);
    }
    _state.PersistAsJson(PersistentStateKeys.UserInfo, userInfo);
    _state.PersistAsJson(PersistentStateKeys.ServerDecommissioned, _serverLifecycleOptions.CurrentValue.DecommissionServer);
  }

  private async Task<AuthenticationState> ProjectClientPoliciesAsync()
  {
    var state = await base.GetAuthenticationStateAsync();

    // A tenant is exactly what GetGrantedPolicies needs to evaluate the tenant-scoped half of the
    // projection, and it always has tenant entries. A server service account principal has none, and
    // reaches here because the dynamic scheme forwards any request carrying an api-key header.
    if (state.User.ToPrincipalDescriptor() is not { TenantId: not null } principal)
    {
      return state;
    }

    await using var scope = _scopeFactory.CreateAsyncScope();
    var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();

    IReadOnlySet<string> grantedPolicies;
    try
    {
      grantedPolicies = await evaluator.GetGrantedPolicies(principal, CancellationToken.None);
    }
    catch (Exception ex)
    {
      // Rendering must survive a failed evaluation. The unprojected state leaves every gate answering
      // false, which is the same fail-closed answer PermissionRequirementHandler returns.
      _logger.LogError(ex, "Could not project client policies for principal {PrincipalId}.", principal.PrincipalId);
      return state;
    }

    // A second identity on a copy of the principal, so HttpContext.User is never mutated. Components
    // read the grants through ClaimsPrincipal.HasClaim, which scans every identity.
    var projected = new ClaimsPrincipal(state.User);
    projected.AddIdentity(new ClaimsIdentity(
      grantedPolicies.Select(policyName => new Claim(PermissionPolicies.ClientPolicyClaimType, policyName)),
      state.User.Identity?.AuthenticationType));

    return new AuthenticationState(projected);
  }

  private async Task<bool> ValidateSecurityStampAsync(UserManager<AppUser> userManager, ClaimsPrincipal principal)
  {
    var user = await userManager.GetUserAsync(principal);
    if (user is null)
    {
      return false;
    }

    if (!userManager.SupportsUserSecurityStamp)
    {
      return true;
    }

    var principalStamp = principal.FindFirstValue(_identityOptions.CurrentValue.ClaimsIdentity.SecurityStampClaimType);
    var userStamp = await userManager.GetSecurityStampAsync(user);
    return principalStamp == userStamp;
  }
}