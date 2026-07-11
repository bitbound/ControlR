using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;
using ControlR.Web.Server.Primitives;

namespace ControlR.Web.Server.Services.Tenants;

public interface ITenantProvisioningService
{
  Task<HttpResult<CreateTenantResponseDto>> CreateTenant(CreateTenantRequestDto request, CancellationToken cancellationToken);
  Task<HttpResult<GetTenantResponseDto>> GetTenant(Guid id, CancellationToken cancellationToken);
}

public class TenantProvisioningService(
  IDbContextFactory<AppDb> dbContextFactory,
  ILogger<TenantProvisioningService> logger) : ITenantProvisioningService
{
  public async Task<HttpResult<CreateTenantResponseDto>> CreateTenant(
    CreateTenantRequestDto request,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return HttpResult.Fail<CreateTenantResponseDto>(HttpResultErrorCode.BadRequest, "Tenant name is required.");
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

      return HttpResult.Ok(new CreateTenantResponseDto(
        tenant.Id,
        tenant.Name ?? string.Empty));
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to provision tenant {TenantName}.", request.Name);
      return HttpResult.Fail<CreateTenantResponseDto>(ex, HttpResultErrorCode.InternalServerError, "Failed to provision tenant.");
    }
  }

  public async Task<HttpResult<GetTenantResponseDto>> GetTenant(Guid id, CancellationToken cancellationToken)
  {
    await using var appDb = await dbContextFactory.CreateDbContextAsync(cancellationToken);

    var tenant = await appDb.Tenants
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    if (tenant is null)
    {
      return HttpResult.Fail<GetTenantResponseDto>(HttpResultErrorCode.NotFound, "Tenant not found.");
    }

    return HttpResult.Ok(new GetTenantResponseDto(
      tenant.Id,
      tenant.Name ?? string.Empty));
  }
}