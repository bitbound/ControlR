using ControlR.Web.Server.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Time.Testing;
using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests.Helpers;

public static class TestAppBuilder
{
  public static async Task<TestApp> CreateTestApp(
    ITestOutputHelper testOutput,
    Action<WebApplicationBuilder>? configure = null)
  {
    Environment.SetEnvironmentVariable("ControlR_AppOptions__UseInMemoryDatabase", "true", EnvironmentVariableTarget.Process);

    var builder = WebApplication.CreateBuilder();
    await builder.AddControlrServer();

    configure?.Invoke(builder);

    var timeProvider = new FakeTimeProvider(DateTimeOffset.Now);
    builder.Services.ReplaceSingleton<TimeProvider, FakeTimeProvider>(timeProvider);

    builder.Logging.ClearProviders();
    builder.Logging.AddProvider(new XunitLoggerProvider(testOutput));

    var app = builder.Build();
    return new TestApp(app, timeProvider);
  }
}