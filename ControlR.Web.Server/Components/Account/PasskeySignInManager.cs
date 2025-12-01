using System;
using Microsoft.AspNetCore.Authentication;

namespace ControlR.Web.Server.Components.Account;

public class PasskeySignInManager(
  UserManager<AppUser> userManager,
  IOptionsMonitor<AppOptions> appOptions,
  IHttpContextAccessor contextAccessor,
  IUserClaimsPrincipalFactory<AppUser> claimsFactory,
  IOptions<IdentityOptions> optionsAccessor,
  ILogger<SignInManager<AppUser>> logger,
  IAuthenticationSchemeProvider schemes,
  IUserConfirmation<AppUser> confirmation)
  : SignInManager<AppUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
  public override async Task<SignInResult> PasskeySignInAsync(string credentialJson)
  {
    ArgumentException.ThrowIfNullOrEmpty(credentialJson);

    var assertionResult = await PerformPasskeyAssertionAsync(credentialJson);
    if (!assertionResult.Succeeded)
    {
      return SignInResult.Failed;
    }

    // After a successful assertion, we need to update the passkey so that it has the latest
    // sign count and authenticator data.
    var setPasskeyResult = await UserManager.AddOrUpdatePasskeyAsync(assertionResult.User, assertionResult.Passkey);
    if (!setPasskeyResult.Succeeded)
    {
      return SignInResult.Failed;
    }
    
    var persistLogin = appOptions.CurrentValue.PersistPasskeyLogin;
    return await SignInOrTwoFactorAsync(assertionResult.User, isPersistent: persistLogin, bypassTwoFactor: true);
  }
}
