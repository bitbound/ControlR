using System.Net;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class ExceptionHandlerIntegrationTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task ApiExceptionHandler_ShouldReturnProblemDetailsJson_ForApiPathExceptions()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      configureServices: services =>
      {
        services.AddSingleton<IStartupFilter, ThrowingMiddlewareFilter>();
      });

    using var client = testServer.Factory.CreateClient();
    using var response = await client.GetAsync(
      "/api/test-throw",
      TestContext.Current.CancellationToken);

    Assert.Equal(
      "application/problem+json",
      response.Content.Headers.ContentType?.MediaType);
  }

  [Fact]
  public async Task UiExceptionHandler_ShouldRedirectToError_ForNonApiExceptions()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      configureServices: services =>
      {
        services.AddSingleton<IStartupFilter, ThrowingMiddlewareFilter>();
      });

    using var client = testServer.Factory.CreateClient(
      new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(
      "/ui/test-throw",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.Equal("/Error", response.Headers.Location?.OriginalString);
  }

  private class ThrowingMiddlewareFilter : IStartupFilter
  {
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
      return app =>
      {
        next(app);
        app.Run(async context =>
        {
          if (context.Request.Path.StartsWithSegments("/api/test-throw") ||
              context.Request.Path.StartsWithSegments("/ui/test-throw"))
          {
            throw new InvalidOperationException("Test exception");
          }
        });
      };
    }
  }
}
