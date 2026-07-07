using System.Security.Claims;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Authz.Roles;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Services.ServiceAccounts;

namespace ControlR.Web.Server.Startup;

public static class HostExtensions
{
  public static async Task AddBuiltInRoles(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    await using var context = scope.ServiceProvider.GetRequiredService<AppDb>();
    var builtInRoles = RoleFactory.GetBuiltInRoles();
    await context.Roles.AddRangeAsync(builtInRoles);
    await context.SaveChangesAsync();
  }

  public static async Task ApplyMigrations(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    await using var context = scope.ServiceProvider.GetRequiredService<AppDb>();
    if (context.Database.IsRelational())
    {
      await context.Database.MigrateAsync();
    }
  }

  public static async Task BootstrapAdminUser(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    var sp = scope.ServiceProvider;

    var appLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
    var bootstrapOptions = sp.GetRequiredService<IOptions<BootstrapOptions>>();
    var options = bootstrapOptions.Value;

    var isEmailSet = !string.IsNullOrWhiteSpace(options.AdminEmail);
    var isPasswordSet = !string.IsNullOrWhiteSpace(options.AdminPassword);

    var logger = sp.GetRequiredService<ILogger<Program>>();

    if (!isEmailSet && !isPasswordSet)
    {
      logger.LogInformation("Bootstrap admin user skipped: Neither AdminEmail nor AdminPassword configured.");
      return;
    }

    if (!isEmailSet || !isPasswordSet)
    {
      logger.LogError(
        "Bootstrap admin configuration incomplete. AdminEmail configured: {EmailIsSet}, AdminPassword configured: {PasswordIsSet}. Both must be set.",
        isEmailSet,
        isPasswordSet);
      throw new InvalidOperationException(
        "Bootstrap admin configuration is incomplete: Both AdminEmail and AdminPassword must be configured.");
    }

    // To satisfy the compiler's null tracking.
    Guard.IsNotNullOrWhiteSpace(options.AdminEmail, nameof(options.AdminEmail));
    Guard.IsNotNullOrWhiteSpace(options.AdminPassword, nameof(options.AdminPassword));

    await using var context = sp.GetRequiredService<AppDb>();

    // SCALE-OUT: Non-transactional check-then-act. In a multi-instance deployment,
    // two instances could both pass AnyAsync() and both attempt CreateAsync,
    // causing a unique constraint failure on the second. Wrap in a database
    // transaction with unique-violation handling when scale-out is needed.
    if (await context.Users.AnyAsync())
    {
      logger.LogInformation("Bootstrap skipped: Users already exist.");
      return;
    }

    var userManager = sp.GetRequiredService<UserManager<AppUser>>();
    var userStore = sp.GetRequiredService<IUserStore<AppUser>>();

    var user = new AppUser();
    var tenant = new Tenant();
    user.Tenant = tenant;

    await userStore.SetUserNameAsync(user, options.AdminEmail, appLifetime.ApplicationStopping);

    if (userStore is not IUserEmailStore<AppUser> emailStore)
    {
      logger.LogError("User store does not implement IUserEmailStore<AppUser>.");
      return;
    }

    await emailStore.SetEmailAsync(user, options.AdminEmail, appLifetime.ApplicationStopping);

    var identityResult = await userManager.CreateAsync(user, options.AdminPassword);
    if (!identityResult.Succeeded)
    {
      foreach (var error in identityResult.Errors)
      {
        logger.LogError(
          "Bootstrap user creation error. Code: {Code}. Description: {Description}",
          error.Code,
          error.Description);
      }
      return;
    }

    logger.LogInformation("Bootstrap admin user created: {Email}.", options.AdminEmail);

    // Assign all built-in roles. When new roles are added to RoleFactory,
    // they are automatically assigned to the bootstrapped admin.
    var builtInRoles = RoleFactory
      .GetBuiltInRoles()
      .Where(x => x.Name is not null)
      .Select(x => x.Name!)
      .ToArray();

    var roleResult = await userManager.AddToRolesAsync(user, builtInRoles);
    if (!roleResult.Succeeded)
    {
      foreach (var error in roleResult.Errors)
      {
        logger.LogError(
          "Bootstrap role assignment error. Code: {Code}. Description: {Description}",
          error.Code,
          error.Description);
      }
      throw new InvalidOperationException($"Bootstrap admin role assignment failed: {string.Join("; ", roleResult.Errors.Select(e => e.Description))}");
    }

    var claimResult = await userManager.AddClaimsAsync(user, [
      new Claim(UserClaimTypes.UserId, $"{user.Id}"),
      new Claim(UserClaimTypes.TenantId, $"{user.TenantId}")
    ]);
    if (!claimResult.Succeeded)
    {
      foreach (var error in claimResult.Errors)
      {
        logger.LogError(
          "Bootstrap claim assignment error. Code: {Code}. Description: {Description}",
          error.Code,
          error.Description);
      }
      throw new InvalidOperationException($"Bootstrap admin claim assignment failed: {string.Join("; ", claimResult.Errors.Select(e => e.Description))}");
    }

    var emailConfirmationCode = await userManager.GenerateEmailConfirmationTokenAsync(user);
    var confirmResult = await userManager.ConfirmEmailAsync(user, emailConfirmationCode);
    if (!confirmResult.Succeeded)
    {
      foreach (var error in confirmResult.Errors)
      {
        logger.LogError(
          "Bootstrap email confirmation error. Code: {Code}. Description: {Description}",
          error.Code,
          error.Description);
      }
      throw new InvalidOperationException($"Bootstrap admin email confirmation failed: {string.Join("; ", confirmResult.Errors.Select(e => e.Description))}");
    }

    if (options.AdminPatTokenId is null || string.IsNullOrWhiteSpace(options.AdminPatSecret))
    {
      logger.LogInformation("Bootstrap admin PAT creation skipped: AdminPatTokenId and AdminPatSecret must both be set.");
    }
    else
    {
      var patManager = sp.GetRequiredService<IPersonalAccessTokenManager>();
      var patResult = await patManager.CreateTokenWithKey(
        options.AdminPatTokenId.Value,
        options.AdminPatSecret,
        "Bootstrap Admin PAT",
        user.Id);

      if (!patResult.IsSuccess)
      {
        logger.LogError(patResult.Exception, "Failed to create bootstrap PAT: {Error}", patResult.Reason);
        throw new InvalidOperationException($"Bootstrap PAT creation failed: {patResult.Reason}");
      }
    }

    logger.LogInformation("Bootstrap admin user setup completed successfully.");
  }

  /// <summary>
  /// Bootstraps the server-scoped service account described by <see cref="BootstrapOptions"/>
  /// (ServerServiceAccountName/TokenId/TokenSecret). No-op when unconfigured; throws on partial
  /// configuration. Safe to call on every startup: creation is skipped when the named account
  /// already exists.
  /// </summary>
  public static async Task BootstrapServerServiceAccount(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    var sp = scope.ServiceProvider;

    var bootstrapOptions = sp.GetRequiredService<IOptions<BootstrapOptions>>();
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var serviceAccountManager = sp.GetRequiredService<IServiceAccountManager>();

    var result = await serviceAccountManager.BootstrapServerServiceAccount(bootstrapOptions.Value);
    if (!result.IsSuccess)
    {
      logger.LogError("Bootstrap server service account failed: {Reason}", result.Reason);
      throw new InvalidOperationException($"Bootstrap server service account failed: {result.Reason}");
    }
  }

  public static async Task RemoveEmptyTenants(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    await using var context = scope.ServiceProvider.GetRequiredService<AppDb>();
    var emptyTenants = await context.Tenants
      .Where(x => x.Users!.Count == 0)
      .ToListAsync();

    if (emptyTenants.Count == 0)
    {
      return;
    }

    context.Tenants.RemoveRange(emptyTenants);
    await context.SaveChangesAsync();
  }

  public static async Task SetAllDevicesOffline(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    await using var context = scope.ServiceProvider.GetRequiredService<AppDb>();
    await context.Devices.ExecuteUpdateAsync(calls => calls.SetProperty(d => d.IsOnline, false));
  }

  public static async Task SetAllUsersOffline(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    await using var context = scope.ServiceProvider.GetRequiredService<AppDb>();
    await context.Users.ExecuteUpdateAsync(calls => calls.SetProperty(d => d.IsOnline, false));
  }
}