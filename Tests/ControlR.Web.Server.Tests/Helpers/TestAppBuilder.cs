using System.Runtime.CompilerServices;
using ControlR.Libraries.TestingUtilities;
using ControlR.Web.Server.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ControlR.Web.Server.Tests.Helpers;

/// <summary>
/// Creates <see cref="TestApp"/> for service-only integration/functional tests.
/// Use <see cref="TestWebServerBuilder"/> for full end-to-end tests involving HTTP requests.
/// </summary>
internal static class TestAppBuilder
{
  public static async Task<TestApp> CreateTestApp(
    ITestOutputHelper testOutput,
    Dictionary<string, string?>? extraConfiguration = null,
    [CallerMemberName] string testDatabaseName = "",
    bool useInMemoryDatabase = true)
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.Now);
    var uniqueDatabaseName = $"{testDatabaseName}-{Guid.NewGuid()}";

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
      EnvironmentName = "Testing",
    });

    if (useInMemoryDatabase)
    {
      _ = builder.Configuration.AddInMemoryCollection(
      [
        new KeyValuePair<string, string?>("AppOptions:UseInMemoryDatabase", $"{useInMemoryDatabase}"),
        new KeyValuePair<string, string?>("AppOptions:InMemoryDatabaseName", uniqueDatabaseName),
      ]);
    }
    else
    {
      var connectionInfo = await PostgresTestContainer.GetConnectionInfo();
      var databaseName = await PostgresTestContainer.CreateDatabase($"{uniqueDatabaseName}");
      _ = builder.Configuration.AddInMemoryCollection(
      [
        new KeyValuePair<string, string?>("AppOptions:UseInMemoryDatabase", $"{useInMemoryDatabase}"),
        new KeyValuePair<string, string?>("AppOptions:InMemoryDatabaseName", string.Empty),
        new KeyValuePair<string, string?>("POSTGRES_USER", connectionInfo.Username),
        new KeyValuePair<string, string?>("POSTGRES_PASSWORD", connectionInfo.Password),
        new KeyValuePair<string, string?>("POSTGRES_HOST", connectionInfo.Host),
        new KeyValuePair<string, string?>("POSTGRES_PORT", $"{connectionInfo.Port}"),
        new KeyValuePair<string, string?>("POSTGRES_DB", databaseName)
      ]);
    }

    if (extraConfiguration is not null)
    {
      builder.Configuration.AddInMemoryCollection(extraConfiguration);
    }

    _ = await builder.AddControlrServer(false);

    _ = builder.Services.ReplaceImplementation<NavigationManager, FakeNavigationManager>(ServiceLifetime.Scoped);

    _ = builder.Services.ReplaceSingleton<TimeProvider, FakeTimeProvider>(timeProvider);
    _ = builder.Logging.ClearProviders();
    _ = builder.Logging.AddProvider(new XunitLoggerProvider(testOutput));

    // Build the app
    var app = builder.Build();
    if (useInMemoryDatabase)
    {
      await app.AddBuiltInRoles();
    }
    else
    {
      await app.ApplyMigrations();
    }

    return new TestApp(timeProvider, app);
  }
}