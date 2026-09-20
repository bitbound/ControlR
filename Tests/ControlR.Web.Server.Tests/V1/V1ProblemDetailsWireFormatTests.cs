using System.Net.Http.Json;
using System.Text;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// The V1 error contract as a caller receives it, which is the only place a response's media type
/// is observable.
/// </summary>
public class V1ProblemDetailsWireFormatTests(ITestOutputHelper testOutput)
{

  /// <summary>
  /// A V1 action that returns a bodyless framework client error, rather than calling Problem() or
  /// going through the shared helper, still has to reach the caller with a body. Nothing else covers
  /// this path, so without it the claim that no V1 failure is a bare status rests on the reader
  /// trusting MVC's client-error mapping.
  /// </summary>
  [Fact]
  public async Task BodylessControllerClientError_AnswersProblemJsonWithTableTitle()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(tenant.Id, $"wire-bare-{Guid.NewGuid():N}@t.local");
    using var client = await CreateAuthenticatedClientAsync(testServer, user);

    // Deleting a key the caller never stored takes the `deleted ? NoContent() : NotFound()` branch,
    // which hands MVC a bare NotFoundResult.
    using var response = await client.DeleteAsync(
      $"{HttpConstants.V1.UserStorageEndpoint}/never-stored-{Guid.NewGuid():N}?tenantId={tenant.Id}",
      TestContext.Current.CancellationToken);

    await AssertProblemWireAsync(response, StatusCodes.Status404NotFound, "Not found.");
  }

  [Fact]
  public async Task ControllerProblemResult_AnswersProblemJsonWithTableTitle()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(tenant.Id, $"wire-prob-{Guid.NewGuid():N}@t.local");
    using var client = await CreateAuthenticatedClientAsync(testServer, user);

    // An unknown device never reaches an agent, so this answer comes straight from the controller's
    // Problem() call rather than from the shared helper.
    using var response = await client.PostAsJsonAsync(
      $"{HttpConstants.V1.DeviceFileSystemEndpoint}/contents?tenantId={tenant.Id}",
      new DeviceDirectoryContentsRequestDto(Guid.NewGuid(), "/parent"),
      TestContext.Current.CancellationToken);

    await AssertProblemWireAsync(response, StatusCodes.Status404NotFound, "Not found.");
  }

  /// <summary>
  /// A request whose body never binds never reaches an action, so no call site can shape its failure.
  /// This validation path is outside the endpoint conventions, and it has to land on the same problem
  /// shape and the same title as the ones a controller writes.
  /// </summary>
  [Fact]
  public async Task ModelBindingFailure_AnswersProblemJsonWithTableTitle()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(
      tenant.Id,
      $"wire-bind-{Guid.NewGuid():N}@t.local",
      PermissionPresets.TenantAdministrator);
    using var client = await CreateAuthenticatedClientAsync(testServer, user);

    using var request = new HttpRequestMessage(
      HttpMethod.Post,
      $"{HttpConstants.V1.TenantSettingsEndpoint}?tenantId={tenant.Id}")
    {
      Content = new StringContent("\"not-an-object\"", Encoding.UTF8, "application/json")
    };

    using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

    await AssertProblemWireAsync(response, StatusCodes.Status400BadRequest, "Invalid request.");
  }

  private static async Task<HttpClient> CreateAuthenticatedClientAsync(
    TestWebServer testServer,
    AppUser user)
  {
    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "V1 wire format PAT",
        PersonalAccessTokenPermissionMode.InheritOwner),
      user.Id,
      new PrincipalDescriptor(PrincipalType.User, user.Id, user.TenantId, "test"));
    Assert.True(patResult.IsSuccess, patResult.Reason);

    var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);
    return client;
  }

  /// <summary>
  /// Asserts the response is a problem+json document whose own status, type and title agree with what
  /// the status line says. The title is asserted by value rather than through the shared constants so
  /// a change to the vocabulary has to be made here too.
  /// </summary>
  private async Task AssertProblemWireAsync(
    HttpResponseMessage response,
    int expectedStatus,
    string expectedTitle)
  {
    Assert.Equal(expectedStatus, (int)response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

    var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    testOutput.WriteLine($"Problem body: {body}");

    var problem = Assert.IsType<ProblemDetails>(
      await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken));

    Assert.Equal(expectedStatus, problem.Status);
    Assert.Equal(expectedTitle, problem.Title);
    Assert.Equal("about:blank", problem.Type);
  }
}
