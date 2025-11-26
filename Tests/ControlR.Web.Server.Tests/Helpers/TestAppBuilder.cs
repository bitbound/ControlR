using System.Runtime.CompilerServices;
using ControlR.Tests.TestingUtilities;
using ControlR.Web.Server.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit.Abstractions;

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
    [CallerMemberName] string testDatabaseName = "")
  {
    var timeProvider = new FakeTimeProvider(DateTimeOffset.Now);

    var builder = WebApplication.CreateBuilder();
    builder.Environment.EnvironmentName = "Testing";
    _ = builder.Configuration.AddInMemoryCollection(
    [
      new KeyValuePair<string, string?>("AppOptions:UseInMemoryDatabase", "true"),
      new KeyValuePair<string, string?>("AppOptions:InMemoryDatabaseName", $"{testDatabaseName}-app")
    ]);

    if (extraConfiguration is not null)
    {
      builder.Configuration.AddInMemoryCollection(extraConfiguration);
    }

    _ = await builder.AddControlrServer(false);

    _ = builder.Services.ReplaceSingleton<TimeProvider, FakeTimeProvider>(timeProvider);
    _ = builder.Logging.ClearProviders();
    _ = builder.Logging.AddProvider(new XunitLoggerProvider(testOutput));

    // Build the app
    var app = builder.Build();
    await app.AddBuiltInRoles();

    return new TestApp(timeProvider, app);
  }
}