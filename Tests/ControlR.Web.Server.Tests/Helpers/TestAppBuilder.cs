using ControlR.Web.Server.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;
using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.Helpers;

public static class TestAppBuilder
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

    // For testing, we need to configure the WebHost for TestServer
    builder.WebHost.UseTestServer();
    
    // Build the app
    var app = builder.Build();
    await app.AddBuiltInRoles();

    // Start the application (required for TestServer)
    await app.StartAsync();

    // Get the TestServer that was registered by UseTestServer() as IServer
    var testServer = (TestServer)app.Services.GetRequiredService<IServer>();
    var httpClient = testServer.CreateClient();

    return new TestApp(app, timeProvider, httpClient, testServer);
  }
}