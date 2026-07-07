using ControlR.Web.Server.Authz.Roles;
using ControlR.Web.Server.Services.Users;

namespace ControlR.Web.Server.Services.Tenants;

public interface ITenantProvisioningService
{
  Task<Result<CreateTenantResponseDto>> CreateTenant(CreateTenantRequestDto request, CancellationToken cancellationToken);
}

public class TenantProvisioningService(
  AppDb appDb,
  IUserCreator userCreator,
  UserManager<AppUser> userManager,
  ILogger<TenantProvisioningService> logger) : ITenantProvisioningService
{
  public async Task<Result<CreateTenantResponseDto>> CreateTenant(
    CreateTenantRequestDto request,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return Result.Fail<CreateTenantResponseDto>("Tenant name is required.");
    }

    if (string.IsNullOrWhiteSpace(request.UserName))
    {
      return Result.Fail<CreateTenantResponseDto>("User name is required.");
    }

    await using var transaction = await appDb.Database.BeginTransactionAsync(cancellationToken);

    try
    {
      var tenant = new Tenant
      {
        Name = request.Name
      };

      appDb.Tenants.Add(tenant);
      await appDb.SaveChangesAsync(cancellationToken);

      var identityName = string.IsNullOrWhiteSpace(request.Email)
        ? request.UserName
        : request.Email;

      var createResult = await userCreator.CreateUser(
        identityName,
        request.Password ?? string.Empty,
        tenant.Id,
        cancellationToken: cancellationToken);

      if (!createResult.Succeeded)
      {
        await transaction.RollbackAsync(cancellationToken);
        var reason = string.Join("; ", createResult.IdentityResult.Errors.Select(x => x.Description));
        return Result.Fail<CreateTenantResponseDto>(reason);
      }

      var user = createResult.User;
      if (user is null)
      {
        await transaction.RollbackAsync(cancellationToken);
        return Result.Fail<CreateTenantResponseDto>("Tenant bootstrap user creation failed.");
      }

      var builtInRoles = RoleFactory
        .GetBuiltInRoles()
        .Where(x => x.Name != RoleNames.ServerAdministrator)
        .Where(x => x.Name is not null)
        .Select(x => x.Name!)
        .ToArray();

      var roleResult = await userManager.AddToRolesAsync(user, builtInRoles);
      if (!roleResult.Succeeded)
      {
        await transaction.RollbackAsync(cancellationToken);
        var reason = string.Join("; ", roleResult.Errors.Select(x => x.Description));
        return Result.Fail<CreateTenantResponseDto>($"Tenant bootstrap role assignment failed: {reason}");
      }

      await transaction.CommitAsync(cancellationToken);

      return Result.Ok(new CreateTenantResponseDto(
        tenant.Id,
        tenant.Name ?? string.Empty,
        user.Id,
        user.UserName ?? request.UserName,
        user.Email));
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to provision tenant {TenantName}.", request.Name);
      await transaction.RollbackAsync(cancellationToken);
      return Result.Fail<CreateTenantResponseDto>(ex, "Failed to provision tenant.");
    }
  }
}