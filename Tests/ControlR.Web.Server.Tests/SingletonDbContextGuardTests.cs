using ControlR.Web.Server.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// <c>AppDb</c> is registered through <c>AddDbContextFactory</c> with a transient lifetime so each
/// caller gets its own short-lived context. A singleton that takes one in its constructor resolves
/// it once and keeps it for the process lifetime, which pins the tenant claims that
/// <c>UseUserClaims</c> read when the context was built and never lets the context be disposed.
/// </summary>
public class SingletonDbContextGuardTests
{
  [Fact]
  public async Task ServerRegistrations_NoSingletonTakesADbContextDependency()
  {
    var descriptors = await GetServerServiceDescriptors();

    var offenders = descriptors
      .Where(x => x.Lifetime == ServiceLifetime.Singleton && x.ImplementationType is not null)
      .Select(x =>
      {
        var implementationType = x.ImplementationType!;
        var dbContextDependencies = implementationType.GetConstructors()
          .SelectMany(c => c.GetParameters())
          .Where(p => typeof(DbContext).IsAssignableFrom(p.ParameterType))
          .Select(p => p.ParameterType.Name)
          .ToList();

        return (Service: x.ServiceType, Implementation: implementationType, DbContextDependencies: dbContextDependencies);
      })
      .Where(x => x.DbContextDependencies.Count > 0)
      .Select(x => $"{x.Implementation.Name} ({x.Service.Name}) takes {string.Join(", ", x.DbContextDependencies)}")
      .ToList();

    Assert.Empty(offenders);
  }

  private static async Task<IReadOnlyList<ServiceDescriptor>> GetServerServiceDescriptors()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
      EnvironmentName = "Testing",
    });

    _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["AppOptions:UseInMemoryDatabase"] = "true",
      ["AppOptions:InMemoryDatabaseName"] = $"dbcontext-guard-{Guid.NewGuid():N}",
    });

    _ = await builder.AddControlrServer(false);

    return [.. builder.Services];
  }
}