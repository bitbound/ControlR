using ControlR.Web.Client.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Security.Claims;

namespace ControlR.Web.Server.Data.Configuration;

public static class DbContextOptionsBuilderExtensions
{
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