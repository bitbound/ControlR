using ControlR.Web.Server.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Time.Testing;
using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

namespace ControlR.Web.Server.Tests.Helpers;

internal static class TestAppBuilder
{
  public static async Task<TestApp> CreateTestApp(
    ITestOutputHelper testOutput,
    [CallerMemberName] string testDatabaseName = "",
    Action<WebApplicationBuilder>? configure = null)
  {
    var builder = WebApplication.CreateBuilder();
    _ = builder.Configuration.AddInMemoryCollection(
    [
      new KeyValuePair<string, string?>("AppOptions:UseInMemoryDatabase", "true"),
      new KeyValuePair<string, string?>("AppOptions:InMemoryDatabaseName", testDatabaseName)
    ]);

    _ = await builder.AddControlrServer(false);

    configure?.Invoke(builder);

    var timeProvider = new FakeTimeProvider(DateTimeOffset.Now);
    _ = builder.Services.ReplaceSingleton<TimeProvider, FakeTimeProvider>(timeProvider);

    _ = builder.Logging.ClearProviders();
    _ = builder.Logging.AddProvider(new XunitLoggerProvider(testOutput));
   
    // Build the app
    var app = builder.Build();
    //await app.AddBuiltInRoles();

    // Get the TestServer for integration/functional tests
    var factory = new WebApplicationFactory<Program>()
      .WithWebHostBuilder(builder =>
      {
        builder.UseEnvironment("Testing");
        builder.UseSetting("AppOptions:UseInMemoryDatabase", "true");
        builder.UseSetting("AppOptions:InMemoryDatabaseName", testDatabaseName);

        builder.ConfigureServices(services =>
        {
          // Replace TimeProvider with FakeTimeProvider in the test server
          _ = services.ReplaceSingleton<TimeProvider, FakeTimeProvider>(timeProvider);
        });

        builder.ConfigureLogging(logging =>
        {
          logging.ClearProviders();
          logging.AddProvider(new XunitLoggerProvider(testOutput));
        });
      });

    return new TestApp(app, timeProvider, factory);
  }
}