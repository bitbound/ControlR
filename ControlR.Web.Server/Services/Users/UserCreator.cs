using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using ControlR.Web.Client.Services;
using ControlR.Web.Server.Authz.Roles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace ControlR.Web.Server.Services.Users;

public interface IUserCreator
{
  Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    string? returnUrl,
    bool isPublicRegistration = false,
    CancellationToken cancellationToken = default);

  Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    Guid tenantId);

  Task<CreateUserResult> CreateUser(
    string emailAddress,
    ExternalLoginInfo externalLoginInfo,
    string? returnUrl,
    bool isPublicRegistration = false,
    CancellationToken cancellationToken = default);
  
  // Overload to create a user within a tenant and optionally assign roles and tags.
  Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    Guid tenantId,
    IEnumerable<Guid>? roleIds = null,
    IEnumerable<Guid>? tagIds = null,
    CancellationToken cancellationToken = default);

  // Overload for API context where NavigationManager is unavailable.
  Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    string? returnUrl,
    string confirmationBaseUrl,
    bool isPublicRegistration = false,
    CancellationToken cancellationToken = default);
}

  public class UserCreator(
    AppDb appDb,
    UserManager<AppUser> userManager,
    NavigationManager navigationManager,
    IUserStore<AppUser> userStore,
    IEmailSender<AppUser> emailSender,
    IOptionsMonitor<AppOptions> appOptions,
    IPublicRegistrationBootstrapGate bootstrapGate,
    IPublicServerSettingsProvider serverSettings,
    ILogger<UserCreator> logger) : IUserCreator
  {
    private readonly AppDb _appDb = appDb;
    private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
    private readonly IPublicRegistrationBootstrapGate _bootstrapGate = bootstrapGate;
    private readonly IEmailSender<AppUser> _emailSender = emailSender;
    private readonly ILogger<UserCreator> _logger = logger;
    private readonly NavigationManager _navigationManager = navigationManager;
    private readonly IPublicServerSettingsProvider _serverSettings = serverSettings;
    private readonly UserManager<AppUser> _userManager = userManager;
    private readonly IUserStore<AppUser> _userStore = userStore;

    private bool DisableFirstUserSelfRegistration => _appOptions.CurrentValue.DisableFirstUserSelfRegistration;

  public async Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    string? returnUrl,
    bool isPublicRegistration = false,
    CancellationToken cancellationToken = default)
  {
    return await CreateUserImpl(
      emailAddress,
      returnUrl: returnUrl,
      password: password,
      isPublicRegistration: isPublicRegistration,
      cancellationToken: cancellationToken);
  }

  public async Task<CreateUserResult> CreateUser(
    string emailAddress,
    ExternalLoginInfo externalLoginInfo,
    string? returnUrl,
    bool isPublicRegistration = false,
    CancellationToken cancellationToken = default)
  {
    return await CreateUserImpl(
      emailAddress,
      returnUrl: returnUrl,
      externalLoginInfo: externalLoginInfo,
      isPublicRegistration: isPublicRegistration,
      cancellationToken: cancellationToken);
  }

  public async Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    Guid tenantId)
  {
    return await CreateUserImpl(
      emailAddress,
      password: password,
      tenantId: tenantId);
  }

  public async Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    Guid tenantId,
    IEnumerable<Guid>? roleIds = null,
    IEnumerable<Guid>? tagIds = null,
    CancellationToken cancellationToken = default)
  {
    var result = await CreateUserImpl(
      emailAddress,
      password: password,
      tenantId: tenantId,
      cancellationToken: cancellationToken);

    if (!result.Succeeded)
    {
      return result;
    }

    var user = result.User;
    if (user is null)
    {
      return new CreateUserResult(false, IdentityResult.Failed(new IdentityError { Description = "User creation failed. No user returned." }));
    }

    // Assign roles if provided
    if (roleIds?.Any() == true)
    {
      var roles = await _appDb.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken: cancellationToken);
      var foundRoleIds = roles.Select(r => r.Id).ToHashSet();
      var missingRoleIds = roleIds.Except(foundRoleIds).ToList();
      if (missingRoleIds.Count != 0)
      {
        await _userManager.DeleteAsync(user);
        var err = new IdentityError { Description = $"Roles not found: {string.Join(',', missingRoleIds)}." };
        return new CreateUserResult(false, IdentityResult.Failed(err));
      }

      foreach (var role in roles)
      {
        if (string.IsNullOrWhiteSpace(role.Name))
        {
          await _userManager.DeleteAsync(user);
          var err = new IdentityError { Description = "Role has no name configured." };
          return new CreateUserResult(false, IdentityResult.Failed(err));
        }

        // Add mapping directly to AspNetUserRoles to avoid relying on RoleManager lookups in tests.
        var exists = await _appDb.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, cancellationToken: cancellationToken);
        if (!exists)
        {
          _appDb.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
          await _appDb.SaveChangesAsync(cancellationToken);
        }
      }
    }

    // Assign tags if provided
    if (tagIds?.Any() == true)
    {
      var tags = await _appDb.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync(cancellationToken: cancellationToken);
      var foundTagIds = tags.Select(t => t.Id).ToHashSet();
      var missingTagIds = tagIds.Except(foundTagIds).ToList();
      if (missingTagIds.Count != 0)
      {
        await _userManager.DeleteAsync(user);
        var err = new IdentityError { Description = $"Tags not found: {string.Join(',', missingTagIds)}." };
        return new CreateUserResult(false, IdentityResult.Failed(err));
      }

      if (tags.Count != 0)
      {
        user.Tags = tags;
        _appDb.Users.Update(user);
        await _appDb.SaveChangesAsync(cancellationToken);
      }
    }

    return new CreateUserResult(true, result.IdentityResult, user);
  }

  public async Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    string? returnUrl,
    string confirmationBaseUrl,
    bool isPublicRegistration = false,
    CancellationToken cancellationToken = default)
  {
    return await CreateUserImpl(
      emailAddress,
      returnUrl: returnUrl,
      password: password,
      confirmationBaseUrl: confirmationBaseUrl,
      isPublicRegistration: isPublicRegistration,
      cancellationToken: cancellationToken);
  }

  private async Task<CreateUserResult> CreateUserImpl(
    string emailAddress,
    string? password = null,
    ExternalLoginInfo? externalLoginInfo = null,
    string? returnUrl = null,
    Guid? tenantId = null,
    string? confirmationBaseUrl = null,
    bool isPublicRegistration = false,
    CancellationToken cancellationToken = default)
  {
    if (isPublicRegistration)
    {
      using var gate = await _bootstrapGate.AcquireAsync(cancellationToken);

      if (!(await _serverSettings.GetPublicServerSettings()).IsPublicRegistrationEnabled)
      {
        _logger.LogWarning(
          "Public registration blocked for {Email}. Registration is not enabled for this instance.",
          emailAddress);

        return new CreateUserResult(
          false,
          IdentityResult.Failed(new IdentityError
          {
            Code = "RegistrationDisabled",
            Description = "Public registration is not currently enabled."
          }));
      }

      return await CreateUserInternal(
        emailAddress, password, externalLoginInfo, returnUrl,
        tenantId, confirmationBaseUrl, cancellationToken);
    }

    return await CreateUserInternal(
      emailAddress, password, externalLoginInfo, returnUrl,
      tenantId, confirmationBaseUrl, cancellationToken);
  }

  private async Task<CreateUserResult> CreateUserInternal(
    string emailAddress,
    string? password = null,
    ExternalLoginInfo? externalLoginInfo = null,
    string? returnUrl = null,
    Guid? tenantId = null,
    string? confirmationBaseUrl = null,
    CancellationToken cancellationToken = default)
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

      await _userStore.SetUserNameAsync(user, emailAddress, cancellationToken);

      if (_userStore is not IUserEmailStore<AppUser> userEmailStore)
      {
        throw new InvalidOperationException("The user store does not implement the IUserEmailStore<AppUser>.");
      }

      await userEmailStore.SetEmailAsync(user, emailAddress, cancellationToken);

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

      var isFirstUser = await _userManager.Users.CountAsync(cancellationToken: cancellationToken) == 1;
      var isServerAdmin = !DisableFirstUserSelfRegistration && isFirstUser;
      if (isServerAdmin)
      {
        _logger.LogInformation(
          "First user created. User: {UserName}. Assigning server administrator role.",
          user.UserName);
        await _userManager.AddToRoleAsync(user, RoleNames.ServerAdministrator);
      }

      await _userManager.AddClaimAsync(user, new Claim(UserClaimTypes.UserId, $"{user.Id}"));
      _logger.LogInformation("Added user's ID claim.");

      await _userManager.AddClaimAsync(user, new Claim(UserClaimTypes.TenantId, $"{user.TenantId}"));
      _logger.LogInformation("Added user's tenant ID claim.");

      if (isNewTenant)
      {
        var rolesToAdd = RoleFactory
          .GetBuiltInRoles()
          .Where(x => x.Name != RoleNames.ServerAdministrator)
          .Where(x => x.Name is not null)
          .Select(x => x.Name!)
          .ToArray();

        await _userManager.AddToRolesAsync(user, rolesToAdd);
        var rolesString = string.Join(", ", rolesToAdd);
        _logger.LogInformation("Assigned default roles for newly-created tenant admin user: {Roles}.", rolesString);
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

      if (_appOptions.CurrentValue.DisableEmailSending && _appOptions.CurrentValue.RequireUserEmailConfirmation)
      {
        throw new InvalidOperationException(
          "Email sending is disabled, but user email confirmation is required. " +
          "Cannot proceed with user creation.");
      }

      if (isNewTenant && !isServerAdmin && !_appOptions.CurrentValue.DisableEmailSending)
      {
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var queryParams = new Dictionary<string, string?>
        {
          ["userId"] = userId,
          ["code"] = code,
          ["returnUrl"] = returnUrl
        };

        var callbackUrl = confirmationBaseUrl is not null
          ? QueryHelpers.AddQueryString(
            $"{confirmationBaseUrl.TrimEnd('/')}/Account/ConfirmEmail",
            queryParams)
          : _navigationManager.GetUriWithQueryParameters(
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