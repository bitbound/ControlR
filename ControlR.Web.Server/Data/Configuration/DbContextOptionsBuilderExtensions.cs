using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Security.Claims;

namespace ControlR.Web.Server.Data.Configuration;

public static class DbContextOptionsBuilderExtensions
{
  /// <summary>
  /// Attaches a <see cref="ClaimsDbContextOptionsExtension"/> to the builder carrying the
  /// authenticated user's tenant and user ids. When the principal is null, unauthenticated,
  /// or lacks those claims (e.g., server service accounts with cross-tenant access), the
  /// extension is still attached with null claims and <see cref="AppDb"/> applies no tenant
  /// query filter. The extension is always attached so EF shares a single internal service
  /// provider (and therefore a single in-memory store and model cache) across every context,
  /// regardless of which principal activated it.
  /// </summary>
  public static DbContextOptionsBuilder UseUserClaims(
      this DbContextOptionsBuilder builder,
      ClaimsPrincipal? user)
  {

    if (builder is not IDbContextOptionsBuilderInfrastructure builderInfrastructure)
    {
      throw new ArgumentException(
          $"Expected {nameof(builder)} to be of type {nameof(IDbContextOptionsBuilderInfrastructure)}");
    }
    
    var claimsOptions = new ClaimsDbContextOptions();
    if (user is { Identity.IsAuthenticated: true } &&
        user.TryGetTenantId(out var tenantId) &&
        user.TryGetUserId(out var userId))
    {
      claimsOptions = new ClaimsDbContextOptions { TenantId = tenantId, UserId = userId };
    }

    builderInfrastructure.AddOrUpdateExtension(new ClaimsDbContextOptionsExtension(claimsOptions));

    return builder;
  }
}