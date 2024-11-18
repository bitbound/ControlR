using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using ControlR.Web.Server.ResultObjects;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace ControlR.Web.Server.Services;

public interface IUserCreator
{
  Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    string? returnUrl);

  Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    Guid tenantId);

  Task<CreateUserResult> CreateUser(
    string emailAddress,
    ExternalLoginInfo externalLoginInfo,
    string? returnUrl);
}

public class UserCreator(
  AppDb appDb,
  UserManager<AppUser> userManager,
  NavigationManager navigationManager,
  IUserStore<AppUser> userStore,
  IEmailSender<AppUser> emailSender,
  ILogger<UserCreator> logger) : IUserCreator
{
  private readonly AppDb _appDb = appDb;
  private readonly IEmailSender<AppUser> _emailSender = emailSender;
  private readonly ILogger<UserCreator> _logger = logger;
  private readonly NavigationManager _navigationManager = navigationManager;
  private readonly UserManager<AppUser> _userManager = userManager;
  private readonly IUserStore<AppUser> _userStore = userStore;

  public async Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    string? returnUrl)
  {
    return await CreateUserImpl(
      emailAddress,
      returnUrl: returnUrl,
      password: password);
  }

  public async Task<CreateUserResult> CreateUser(
    string emailAddress,
    ExternalLoginInfo externalLoginInfo,
    string? returnUrl)
  {
    return await CreateUserImpl(
      emailAddress,
      returnUrl: returnUrl,
      externalLoginInfo: externalLoginInfo);
  }

  public async Task<CreateUserResult> CreateUser(string emailAddress, string password, Guid tenantId)
  {
    return await CreateUserImpl(
      emailAddress,
      password: password,
      tenantId: tenantId);
  }

  private async Task<CreateUserResult> CreateUserImpl(
    string emailAddress,
    string? password = null,
    ExternalLoginInfo? externalLoginInfo = null,
    string? returnUrl = null,
    Guid? tenantId = null)
  {
    try
    {
      var isNewTenant = tenantId is null;
      var user = new AppUser();

      if (tenantId is not null)
      {
        user.TenantId = tenantId.Value;
      }
      else
      {
        var tenant = new Tenant();
        user.Tenant = tenant;
      }

      await _userStore.SetUserNameAsync(user, emailAddress, CancellationToken.None);

      if (_userStore is not IUserEmailStore<AppUser> userEmailStore)
      {
        throw new InvalidOperationException("The user store does not implement the IUserEmailStore<AppUser>.");
      }

      await userEmailStore.SetEmailAsync(user, emailAddress, CancellationToken.None);

      var identityResult = string.IsNullOrWhiteSpace(password)
        ? await _userManager.CreateAsync(user)
        : await _userManager.CreateAsync(user, password);

      if (!identityResult.Succeeded)
      {
        foreach (var error in identityResult.Errors)
        {
          _logger.LogError(
            "Identity error occurred while creating user.  Code: {Code}. Description: {Description}",
            error.Code,
            error.Description);
        }

        return new CreateUserResult(false, identityResult, user);
      }

      _logger.LogInformation("Created new account: {Email}.", emailAddress);

      if (await _userManager.Users.CountAsync() == 1)
      {
        _logger.LogInformation("First user created. User: {UserName}. Assigning server administrator role.",
          user.UserName);
        await _userManager.AddToRoleAsync(user, RoleNames.ServerAdministrator);
      }

      await _userManager.AddClaimAsync(user, new Claim(UserClaimTypes.UserId, $"{user.Id}"));
      _logger.LogInformation("Added user's ID claim.");

      await _userManager.AddClaimAsync(user, new Claim(UserClaimTypes.TenantId, $"{user.TenantId}"));
      _logger.LogInformation("Added user's tenant ID claim.");

      if (isNewTenant)
      {
        await _userManager.AddToRoleAsync(user, RoleNames.TenantAdministrator);
        _logger.LogInformation("Assigned user role TenantAdministrator for newly-created tenant.");

        await _userManager.AddToRoleAsync(user, RoleNames.DeviceSuperUser);
        _logger.LogInformation("Assigned user role DeviceSuperUser for newly-created tenant.");
      }

      if (externalLoginInfo is not null)
      {
        var addLoginResult = await _userManager.AddLoginAsync(user, externalLoginInfo);
        if (!addLoginResult.Succeeded)
        {
          return new CreateUserResult(false, addLoginResult);
        }

        _logger.LogInformation("User created an account using {Name} provider.", externalLoginInfo.LoginProvider);
      }

      var userId = await _userManager.GetUserIdAsync(user);
      var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

      if (isNewTenant)
      {
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = _navigationManager.GetUriWithQueryParameters(
          _navigationManager.ToAbsoluteUri("Account/ConfirmEmail").AbsoluteUri,
          new Dictionary<string, object?> { ["userId"] = userId, ["code"] = code, ["returnUrl"] = returnUrl });

        await _emailSender.SendConfirmationLinkAsync(user, emailAddress, HtmlEncoder.Default.Encode(callbackUrl));
      }
      else
      {
        await _userManager.ConfirmEmailAsync(user, code);
      }

      return new CreateUserResult(true, identityResult, user);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating user.");
      var identityError = new IdentityError
      {
        Code = string.Empty,
        Description = ex.Message
      };
      return new CreateUserResult(false, IdentityResult.Failed(identityError));
    }
  }
}