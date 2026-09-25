using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using ControlR.Web.Client.Services;
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
  
  // Overload to create a user within a tenant and optionally assign permission presets.
  Task<CreateUserResult> CreateUser(
    string emailAddress,
    string password,
    Guid tenantId,
    IEnumerable<string>? presetNames = null,
    CancellationToken cancellationToken = default);
}

  public class UserCreator(
    IPermissionAssignmentSeeder assignmentSeeder,
    UserManager<AppUser> userManager,
    IPublicUrlProvider publicUrlProvider,
    IUserStore<AppUser> userStore,
    IEmailSender<AppUser> emailSender,
    IOptionsMonitor<AppOptions> appOptions,
    IPublicRegistrationBootstrapGate bootstrapGate,
    IPublicServerSettingsProvider serverSettings,
    ILogger<UserCreator> logger) : IUserCreator
  {
    public const string ConfirmationEmailUnavailableErrorCode = "ConfirmationEmailUnavailable";
    public const string PresetsNotFoundErrorCode = "PresetsNotFound";
    public const string RegistrationDisabledErrorCode = "RegistrationDisabled";

    private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
    private readonly IPermissionAssignmentSeeder _assignmentSeeder = assignmentSeeder;
    private readonly IPublicRegistrationBootstrapGate _bootstrapGate = bootstrapGate;
    private readonly IEmailSender<AppUser> _emailSender = emailSender;
    private readonly ILogger<UserCreator> _logger = logger;
    private readonly IPublicUrlProvider _publicUrlProvider = publicUrlProvider;
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
    IEnumerable<string>? presetNames = null,
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

    // Assign permission presets if provided.
    if (presetNames?.Any() == true)
    {
      var missingPresets = presetNames.Except(PermissionPresets.All.Keys).ToList();
      if (missingPresets.Count != 0)
      {
        await _userManager.DeleteAsync(user);
        var err = new IdentityError
        {
          Code = PresetsNotFoundErrorCode,
          Description = $"Presets not found: {string.Join(',', missingPresets)}."
        };
        return new CreateUserResult(false, IdentityResult.Failed(err));
      }

      await _assignmentSeeder.SeedAssignments(user.Id, user.TenantId, presetNames, cancellationToken);
    }

    return new CreateUserResult(true, result.IdentityResult, user);
  }

  private async Task<CreateUserResult> CreateUserImpl(
    string emailAddress,
    string? password = null,
    ExternalLoginInfo? externalLoginInfo = null,
    string? returnUrl = null,
    Guid? tenantId = null,
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
            Code = RegistrationDisabledErrorCode,
            Description = "Public registration is not currently enabled."
          }));
      }

      return await CreateUserInternal(
        emailAddress, password, externalLoginInfo, returnUrl,
        tenantId, cancellationToken);
    }

    return await CreateUserInternal(
      emailAddress, password, externalLoginInfo, returnUrl,
      tenantId, cancellationToken);
  }

  private async Task<CreateUserResult> CreateUserInternal(
    string emailAddress,
    string? password = null,
    ExternalLoginInfo? externalLoginInfo = null,
    string? returnUrl = null,
    Guid? tenantId = null,
    CancellationToken cancellationToken = default)
  {
    try
    {
      if (_appOptions.CurrentValue.DisableEmailSending && _appOptions.CurrentValue.RequireUserEmailConfirmation)
      {
        throw new InvalidOperationException(
          "Email sending is disabled, but user email confirmation is required. " +
          "Cannot proceed with user creation.");
      }

      var isNewTenant = tenantId is null;
      var isFirstUser = !await _userManager.Users.AnyAsync(cancellationToken);
      var isServerAdmin = !DisableFirstUserSelfRegistration && isFirstUser;

      // A brand-new tenant that isn't started by the first user receives its confirmation link by
      // email. Everything that email depends on is verified here, before the account exists, so a
      // missing origin fails without leaving a created user behind.
      var needsConfirmationEmail =
        isNewTenant && !isServerAdmin && !_appOptions.CurrentValue.DisableEmailSending;

      if (needsConfirmationEmail && !_publicUrlProvider.HasTrustworthyOrigin)
      {
        _logger.LogError(
          "Cannot send a confirmation email for {Email}. This server has no trustworthy origin to " +
          "build the link from, so set AppOptions:PublicBaseUrl.",
          emailAddress);

        return new CreateUserResult(
          false,
          IdentityResult.Failed(new IdentityError
          {
            Code = ConfirmationEmailUnavailableErrorCode,
            Description = "This server cannot send confirmation emails. Contact an administrator."
          }));
      }

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

      if (isServerAdmin)
      {
        _logger.LogInformation(
          "First user created. User: {UserName}. Assigning server administrator preset.",
          user.UserName);
        await _assignmentSeeder.SeedAssignments(
          user.Id, user.TenantId, [PermissionPresets.ServerAdministrator], cancellationToken);
      }

      await _userManager.AddClaimAsync(user, new Claim(UserClaimTypes.UserId, $"{user.Id}"));
      _logger.LogInformation("Added user's ID claim.");

      await _userManager.AddClaimAsync(user, new Claim(UserClaimTypes.TenantId, $"{user.TenantId}"));
      _logger.LogInformation("Added user's tenant ID claim.");

      if (isNewTenant)
      {
        _logger.LogInformation("Assigning default presets for newly-created tenant admin user.");
        await _assignmentSeeder.SeedAssignments(
          user.Id,
          user.TenantId,
          [
            PermissionPresets.TenantAdministrator,
            PermissionPresets.DeviceSuperUser,
            PermissionPresets.AgentInstaller,
            PermissionPresets.InstallerKeyManager,
          ],
          cancellationToken);
      }

      // Every interactive user gets the self-service baseline (manage own PATs). Seeded for
      // all UserCreator paths. The seeder dedupes against preset grants.
      await _assignmentSeeder.SeedUserBaseline(user.Id, user.TenantId, cancellationToken);

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

      if (needsConfirmationEmail)
      {
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var callbackUrl = _publicUrlProvider.TryGetAbsoluteUrl(
          "Account/ConfirmEmail",
          new Dictionary<string, string?>
          {
            ["userId"] = userId,
            ["code"] = code,
            ["returnUrl"] = returnUrl
          });

        // HasTrustworthyOrigin was verified before the account was created, so a null here means
        // the origin became untrustworthy mid-request.
        ArgumentNullException.ThrowIfNull(callbackUrl);

        await _emailSender.SendConfirmationLinkAsync(
          user, emailAddress, HtmlEncoder.Default.Encode(callbackUrl));
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