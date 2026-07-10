using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

public class ExceptionHandlerIntegrationTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task ApiController_NotFoundResponse_ShouldReturnProblemDetailsJson()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);

    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(tenant.Id, "pat-404-test@t.local");

    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new CreatePersonalAccessTokenRequestDto("404 Test PAT"),
      user.Id);
    Assert.True(patResult.IsSuccess, patResult.Reason);

    using var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);

    var randomGuid = Guid.NewGuid();
    using var response = await client.GetAsync(
      $"{HttpConstants.Internal.DevicesEndpoint}/{randomGuid}",
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal(
      "application/problem+json",
      response.Content.Headers.ContentType?.MediaType);

    var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.NotNull(problemDetails);
    Assert.Equal(404, problemDetails.Status);
    Assert.Equal("Not Found", problemDetails.Title);
    Assert.NotNull(problemDetails.Type);
  }

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

    var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.NotNull(problemDetails);
    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.Equal("An unexpected error occurred.", problemDetails.Title);
    Assert.Equal(500, problemDetails.Status);
    Assert.Equal("An unexpected error occurred.", problemDetails.Detail);
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
