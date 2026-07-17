using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Security.Claims;

namespace ControlR.Web.Server.Data.Configuration;

public static class DbContextOptionsBuilderExtensions
{
  /// <summary>
  /// Applies tenant and user scoping to the DbContext based on the authenticated user's claims.
  /// When the <c>controlr:tenant:id</c> claim is absent (e.g., for server service accounts with
  /// cross-tenant access), no <see cref="ClaimsDbContextOptionsExtension"/> is added to the options,
  /// and <see cref="AppDb"/> applies no tenant query filter. This explicit contract enables
  /// cross-tenant queries for server principals.
  /// </summary>
  public static DbContextOptionsBuilder UseUserClaims(
      this DbContextOptionsBuilder builder,
      ClaimsPrincipal user)
  {
    if (!user.TryGetTenantId(out var tenantId) ||
        !user.TryGetUserId(out var userId))
    {
      return builder;
    }

    var options = new ClaimsDbContextOptions
    {
      TenantId = tenantId,
      UserId = userId
    };

    if (builder is not IDbContextOptionsBuilderInfrastructure builderInfrastructure)
    {
      throw new ArgumentException(
          $"Expected {nameof(builder)} to be of type {nameof(IDbContextOptionsBuilderInfrastructure)}");
    }

    builderInfrastructure.AddOrUpdateExtension(new ClaimsDbContextOptionsExtension(options));

    return builder;
  }
}