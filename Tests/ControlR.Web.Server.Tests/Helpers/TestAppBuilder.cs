using ControlR.Web.Server.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Time.Testing;
using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;

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

    var app = builder.Build();
    await app.AddBuiltInRoles();

    return new TestApp(app, timeProvider);
  }
}