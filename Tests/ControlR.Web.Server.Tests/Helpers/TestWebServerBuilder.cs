using ControlR.Libraries.TestingUtilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using System.Runtime.CompilerServices;
using Xunit;

namespace ControlR.Web.Server.Tests.Helpers;

/// <summary>
/// Creates a TestServer for full end-to-end integration/functional tests.
/// Use <see cref="TestAppBuilder"/> for service-only tests.
/// </summary>
internal static class TestWebServerBuilder
{
  public static async Task<TestWebServer> CreateTestServer(
    ITestOutputHelper testOutput,
    [CallerMemberName] string testDatabaseName = "",
    bool useInMemoryDatabase = true,
    IReadOnlyDictionary<string, string?>? settings = null,
    Action<IServiceCollection>? configureServices = null)
  {
    WebApplicationFactory<Program>? factory;
    var timeProvider = new FakeTimeProvider(DateTimeOffset.Now);
    var uniqueDatabaseName = $"{testDatabaseName}-{Guid.NewGuid()}";

    if (useInMemoryDatabase)
    {
      factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
          builder.UseEnvironment("Testing");
          builder.UseSetting("AppOptions:UseInMemoryDatabase", $"{useInMemoryDatabase}");
          builder.UseSetting("AppOptions:InMemoryDatabaseName", uniqueDatabaseName);
          ApplySettings(builder, settings);

          builder.ConfigureTestServices(timeProvider, testOutput, configureServices);
        });
    }
    else
    {
      var connectionInfo = await PostgresTestContainer.GetConnectionInfo();
      var databaseName = await PostgresTestContainer.CreateDatabase($"{uniqueDatabaseName}");

      factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
          builder.UseEnvironment("Testing");
          builder.UseSetting("AppOptions:UseInMemoryDatabase", $"{useInMemoryDatabase}");
          builder.UseSetting("AppOptions:InMemoryDatabaseName", string.Empty);
          builder.UseSetting("POSTGRES_USER", connectionInfo.Username);
          builder.UseSetting("POSTGRES_PASSWORD", connectionInfo.Password);
          builder.UseSetting("POSTGRES_HOST", connectionInfo.Host);
          builder.UseSetting("POSTGRES_PORT", $"{connectionInfo.Port}");
          builder.UseSetting("POSTGRES_DB", databaseName);
          ApplySettings(builder, settings);

          builder.ConfigureTestServices(timeProvider, testOutput, configureServices);
        });
    }


    return new TestWebServer(timeProvider, factory);
  }

  private static void ApplySettings(IWebHostBuilder builder, IReadOnlyDictionary<string, string?>? settings)
  {
    if (settings is null)
    {
      return;
    }

    foreach (var (key, value) in settings)
    {
      builder.UseSetting(key, value);
    }
  }

  private static IWebHostBuilder ConfigureTestServices(
    this IWebHostBuilder builder,
    FakeTimeProvider timeProvider,
    ITestOutputHelper testOutput,
    Action<IServiceCollection>? configureServices = null)
  {
    builder.ConfigureServices(services =>
    {
      _ = services.ReplaceSingleton<TimeProvider, FakeTimeProvider>(timeProvider);
      configureServices?.Invoke(services);
    });

    builder.ConfigureLogging(logging =>
    {
      logging.ClearProviders();
      logging.AddProvider(new XunitLoggerProvider(testOutput));
    });

    return builder;
  }
}