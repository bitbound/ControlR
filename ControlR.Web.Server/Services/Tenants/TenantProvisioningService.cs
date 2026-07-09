namespace ControlR.Web.Server.Services.Tenants;

public interface ITenantProvisioningService
{
  Task<Result<CreateTenantResponseDto>> CreateTenant(CreateTenantRequestDto request, CancellationToken cancellationToken);
}

public class TenantProvisioningService(
  IDbContextFactory<AppDb> dbContextFactory,
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

    try
    {
      await using var appDb = await dbContextFactory.CreateDbContextAsync(cancellationToken);
      
      var tenant = new Tenant
      {
        Name = request.Name
      };
        
      appDb.Tenants.Add(tenant);
      await appDb.SaveChangesAsync(cancellationToken);

      return Result.Ok(new CreateTenantResponseDto(
        tenant.Id,
        tenant.Name ?? string.Empty));
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to provision tenant {TenantName}.", request.Name);
      return Result.Fail<CreateTenantResponseDto>(ex, "Failed to provision tenant.");
    }
  }
}