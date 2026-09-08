using System.Security.Claims;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Options;
using ControlR.Web.Server.Services.Users;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControlR.Web.Server.Tests;

public class UserCreatorTests(ITestOutputHelper output)
{
    [Fact]
    public async Task CreateUser_DuplicateEmail_Fails()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

        await userCreator.CreateUser("duplicate@example.com", "Password123!", null, cancellationToken: TestContext.Current.CancellationToken);
        var result = await userCreator.CreateUser("duplicate@example.com", "Password123!", null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(result.IdentityResult.Errors, e => e.Code == "DuplicateUserName");
    }

    [Fact]
    public async Task CreateUser_EmailSendingDisabled_ConfirmationNotRequired_SucceedsAndConfirmsEmail()
    {
        var config = new Dictionary<string, string?>
        {
            ["AppOptions:DisableEmailSending"] = "true",
            ["AppOptions:RequireUserEmailConfirmation"] = "false"
        };

        await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

        var result = await userCreator.CreateUser("noconfirm@example.com", "Password123!", null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.User!.EmailConfirmed);
    }

    [Fact]
    public async Task CreateUser_EmailSendingDisabled_ConfirmationRequired_Throws()
    {
        var config = new Dictionary<string, string?>
        {
            ["AppOptions:DisableEmailSending"] = "true",
            ["AppOptions:RequireUserEmailConfirmation"] = "true"
        };

        await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
        using var scope = testApp.CreateScope();
        
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<AppOptions>>();
        output.WriteLine($"DisableEmailSending: {options.CurrentValue.DisableEmailSending}");
        output.WriteLine($"RequireUserEmailConfirmation: {options.CurrentValue.RequireUserEmailConfirmation}");

        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

        var result = await userCreator.CreateUser("throw@example.com", "Password123!", null, cancellationToken: TestContext.Current.CancellationToken);
            
        Assert.False(result.Succeeded);
        Assert.Contains(result.IdentityResult.Errors, e => e.Description.Contains("Email sending is disabled"));
    }

    [Fact]
    public async Task CreateUser_ExistingTenant_DoesNotAssignAdminPresets()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

        // Create first user so next one isn't server admin
        await userCreator.CreateUser("admin@example.com", "Password123!", null, cancellationToken: TestContext.Current.CancellationToken);

        var tenant = new Tenant { Name = "Existing Tenant" };
        appDb.Tenants.Add(tenant);
        await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await userCreator.CreateUser("user@example.com", "Password123!", tenant.Id);

        Assert.True(result.Succeeded);
        var permissions = await appDb.PermissionAssignments
          .Where(x => x.PrincipalId == result.User!.Id)
          .Select(x => x.PermissionName)
          .ToListAsync(TestContext.Current.CancellationToken);

        // A user in an existing tenant gets only the self-service baseline (manage own PATs),
        // which preserves the pre-permissions behavior. No admin/device presets are seeded.
        var expectedSelfService = PermissionPresets
          .GetPermissions(PermissionPresets.SelfService)
          .Order()
          .ToArray();
        Assert.Equal(expectedSelfService, permissions.Order().ToArray());
    }

    [Fact]
    public async Task CreateUser_FirstUser_IsServerAdmin()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

        var result = await userCreator.CreateUser("admin@example.com", "Password123!", null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var permissions = await appDb.PermissionAssignments
          .Where(x => x.PrincipalId == result.User!.Id)
          .Select(x => x.PermissionName)
          .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(PermissionNames.ServerPermissionsWrite, permissions);
    }

    [Fact]
    public async Task CreateUser_MissingPresets_FailsAndCleansUp()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

        var tenant = new Tenant { Name = "Test Tenant" };
        appDb.Tenants.Add(tenant);
        await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await userCreator.CreateUser(
            "fail@example.com", 
            "Password123!", 
            tenant.Id, 
            presetNames: ["Nonexistent Preset"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(result.IdentityResult.Errors, e => e.Code == UserCreator.PresetsNotFoundErrorCode);

        // Verify user was deleted
        var user = await appDb.Users.FirstOrDefaultAsync(u => u.Email == "fail@example.com", TestContext.Current.CancellationToken);
        Assert.Null(user);
    }

    [Fact]
    public async Task CreateUser_NewTenant_AssignsDefaultPresets()
    {
        var config = new Dictionary<string, string?>
        {
            ["AppOptions:DisableEmailSending"] = "true"
        };

        await using var testApp = await TestAppBuilder.CreateTestApp(output, extraConfiguration: config);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

        // Create first user so next one isn't server admin
        await userCreator.CreateUser("admin@example.com", "Password123!", null, cancellationToken: TestContext.Current.CancellationToken);

        var result = await userCreator.CreateUser("tenantadmin@example.com", "Password123!", null, cancellationToken: TestContext.Current.CancellationToken);

        if (!result.Succeeded)
        {
             output.WriteLine($"CreateUser failed: {string.Join(", ", result.IdentityResult.Errors.Select(e => e.Description))}");
        }
        Assert.True(result.Succeeded);
        var permissions = await appDb.PermissionAssignments
          .Where(x => x.PrincipalId == result.User!.Id)
          .Select(x => x.PermissionName)
          .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(PermissionNames.TenantSettingsWrite, permissions);
        Assert.Contains(PermissionNames.DeviceRead, permissions);
        Assert.Contains(PermissionNames.AgentInstall, permissions);
        Assert.DoesNotContain(PermissionNames.ServerPermissionsWrite, permissions);
    }

    [Fact]
    public async Task CreateUser_WithExternalLogin_Succeeds()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

        var email = "external@example.com";
        var loginInfo = new ExternalLoginInfo(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "123")], "TestAuth")),
            "TestProvider",
            "123",
            "Test User");

        var result = await userCreator.CreateUser(email, loginInfo, null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal(email, result.User.Email);
        
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var logins = await userManager.GetLoginsAsync(result.User);
        Assert.Contains(logins, l => l.LoginProvider == "TestProvider" && l.ProviderKey == "123");
    }
    [Fact]
    public async Task CreateUser_WithPassword_Succeeds()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();

        var email = "test@example.com";
        var result = await userCreator.CreateUser(email, "Password123!", null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal(email, result.User.Email);
        Assert.Equal(email, result.User.UserName);
    }

    [Fact]
    public async Task CreateUser_WithPresets_Succeeds()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

        // Create tenant
        var tenant = new Tenant { Name = "Test Tenant" };
        appDb.Tenants.Add(tenant);
        await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        var email = "complex@example.com";
        var result = await userCreator.CreateUser(
            email, 
            "Password123!", 
            tenant.Id, 
            presetNames: [PermissionPresets.DeviceSuperUser],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var user = result.User;
        Assert.NotNull(user);

        // Verify the preset's permissions were assigned.
        var hasDeviceRead = await appDb.PermissionAssignments.AnyAsync(
            x => x.PrincipalId == user.Id && x.PermissionName == PermissionNames.DeviceRead,
            TestContext.Current.CancellationToken);
        Assert.True(hasDeviceRead);
    }

    [Fact]
    public async Task CreateUser_WithTenantId_Succeeds()
    {
        await using var testApp = await TestAppBuilder.CreateTestApp(output);
        using var scope = testApp.CreateScope();
        var userCreator = scope.ServiceProvider.GetRequiredService<IUserCreator>();
        await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

        // Create a tenant first
        var tenant = new Tenant { Name = "Test Tenant" };
        appDb.Tenants.Add(tenant);
        await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        var email = "tenantuser@example.com";
        var result = await userCreator.CreateUser(email, "Password123!", tenant.Id);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal(tenant.Id, result.User.TenantId);
    }
}