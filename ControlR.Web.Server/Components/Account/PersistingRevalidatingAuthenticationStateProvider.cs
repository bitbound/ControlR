using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Server.Components.Account;

// This is a server-side AuthenticationStateProvider that revalidates the security stamp for the connected user
// every 30 minutes an interactive circuit is connected. It also uses PersistentComponentState to flow the
// authentication state to the client which is then fixed for the lifetime of the WebAssembly application.
internal sealed class PersistingRevalidatingAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
  private readonly ILogger<PersistingRevalidatingAuthenticationStateProvider> _logger;
  private readonly IdentityOptions _options;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly PersistentComponentState _state;
  private readonly PersistingComponentStateSubscription _subscription;

  private Task<AuthenticationState>? _authenticationStateTask;

  public PersistingRevalidatingAuthenticationStateProvider(
    PersistentComponentState persistentComponentState,
    ILoggerFactory loggerFactory,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<PersistingRevalidatingAuthenticationStateProvider> logger)
    : base(loggerFactory)
  {
    _scopeFactory = serviceScopeFactory;
    _state = persistentComponentState;
    _options = optionsAccessor.Value;
    _logger = logger;

    AuthenticationStateChanged += OnAuthenticationStateChanged;
    _subscription = _state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
  }

  protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

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

  private static async Task AddSelfRegistrationClaims(IServiceScope scope, UserInfo userInfo)
  {
    var regProvider = scope.ServiceProvider.GetRequiredService<IUserRegistrationProvider>();
    if (await regProvider.IsSelfRegistrationEnabled())
    {
      userInfo.Claims.Add(new UserClaim() { Type = UserClaimTypes.CanSelfRegister, Value = "" });
    }
  }

  private async Task AddDbRolesAndClaims(AsyncServiceScope scope, UserInfo userInfo, ClaimsPrincipal principal)
  {
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var user = await userManager.GetUserAsync(principal);

    if (user is not null)
    {
      var userRoles = await userManager.GetRolesAsync(user);
      var claims = await userManager.GetClaimsAsync(user);
      var userClaims = claims.Select(x => new UserClaim()
      {
        Type = x.Type,
        Value = x.Value
      });

      userInfo.Claims.AddRange(userClaims);
      userInfo.Roles.AddRange(userRoles);
    }
    else
    {
      _logger.LogCritical(
        "User is authenticated but not found in the database. Username: {UserName}",
        principal.Identity?.Name);
    }
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

    await using var scope = _scopeFactory.CreateAsyncScope();

    var authenticationState = await _authenticationStateTask;
    var principal = authenticationState.User;
    var isAuthenticated = principal.Identity?.IsAuthenticated == true;
    var userId = principal.FindFirst(_options.ClaimsIdentity.UserIdClaimType)?.Value;
    var email = principal.FindFirst(_options.ClaimsIdentity.EmailClaimType)?.Value;
    var userInfo = new UserInfo(isAuthenticated)
    {
      UserId = userId ?? string.Empty,
      Email = email ?? string.Empty
    };

    await AddSelfRegistrationClaims(scope, userInfo);
    
    if (isAuthenticated)
    {
      await AddDbRolesAndClaims(scope, userInfo, principal);
    }

    _state.PersistAsJson(nameof(UserInfo), userInfo);
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

    var principalStamp = principal.FindFirstValue(_options.ClaimsIdentity.SecurityStampClaimType);
    var userStamp = await userManager.GetSecurityStampAsync(user);
    return principalStamp == userStamp;
  }
}