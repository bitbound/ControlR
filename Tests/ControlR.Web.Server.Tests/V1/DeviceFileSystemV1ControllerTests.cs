using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Services.DeviceFileSystem;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// The device file system operations on the versioned controller. These assert the V1 contract: a
/// required tenantId resolved once per action, the resolved tenant applied to the device load as an
/// explicit predicate, and the uniform status mapping the deprecated internal endpoints lack (missing
/// device 404, agent refusal 409 carrying the agent's text, no answer 502, canceled wait 408). The
/// four binary siblings stay internal until the API client can carry a streamed result.
/// <para>
/// Test names are prefixed with the action method name, which is also the grouping, since member
/// ordering keeps them alphabetical.
/// </para>
/// <para>
/// The harness <c>AppDb</c> has no <c>HttpContext</c>, so <c>UseUserClaims</c> leaves the
/// claims-driven tenant filter inactive in every test here, caller and server principal alike. The
/// explicit predicate on the device load is therefore what keeps a foreign device out, and these tests
/// pin it. Production is the opposite ordering: a tenant-bound caller's filter removes the row first,
/// while a server principal receives an unfiltered context where the predicate is the only boundary.
/// </para>
/// </summary>
public class DeviceFileSystemV1ControllerTests(ITestOutputHelper testOutput)
{
  private const string DeviceOfflineMessage = "Device is not currently online.";
  private const string OnlineConnectionId = "test-agent-connection-id";

  private readonly ITestOutputHelper _testOutput = testOutput;

  /// <summary>
  /// <see cref="RequireTenantIdActionConvention"/> adds the empty-id 400 to every V1 action that takes a
  /// tenantId. This pins that all eight take the parameter.
  /// </summary>
  [Fact]
  public void AllActions_TakeARequiredTenantIdParameter()
  {
    var actions = typeof(DeviceFileSystemController)
      .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
      .Where(x => x.IsDefined(typeof(HttpMethodAttribute), inherit: false))
      .ToArray();

    Assert.Equal(8, actions.Length);

    foreach (var action in actions)
    {
      var tenantId = Assert.Single(
        action.GetParameters(),
        x => x.Name == "tenantId" && x.ParameterType == typeof(Guid));

      Assert.False(tenantId.HasDefaultValue);
    }
  }

  /// <summary>
  /// The convention matches the parameter by name and type, so a rename would silently drop the 400.
  /// Driving every action through the real pipeline pins that the filter attached.
  /// </summary>
  [Fact]
  public async Task AllActions_WhenTenantIdIsEmpty_ReturnBadRequestBeforeTheActionBody()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    using var httpClient = await CreateAuthenticatedClient(testServer, tenant.Id);
    var deviceId = Guid.NewGuid();
    var emptyTenant = Guid.Empty;

    var requests = new (HttpMethod Method, string Path, object? Body)[]
    {
      (HttpMethod.Post, $"create-directory/{deviceId}?tenantId={emptyTenant}", new CreateDeviceDirectoryRequestDto("/parent", "new-dir")),
      (HttpMethod.Delete, $"delete-path/{deviceId}?tenantId={emptyTenant}", new DeleteDevicePathRequestDto("/parent/file.txt")),
      (HttpMethod.Post, $"contents?tenantId={emptyTenant}", new DeviceDirectoryContentsRequestDto(deviceId, "/parent")),
      (HttpMethod.Get, $"logs/{deviceId}?tenantId={emptyTenant}", null),
      (HttpMethod.Post, $"path-segments?tenantId={emptyTenant}", new DevicePathSegmentsRequestDto(deviceId, "/parent/child")),
      (HttpMethod.Post, $"root-drives?tenantId={emptyTenant}", new DeviceRootDrivesRequestDto(deviceId)),
      (HttpMethod.Post, $"subdirectories?tenantId={emptyTenant}", new DeviceSubdirectoriesRequestDto(deviceId, "/parent")),
      (HttpMethod.Post, $"validate-path/{deviceId}?tenantId={emptyTenant}", new ValidateDeviceFilePathRequestDto("/parent", "file.txt")),
    };

    foreach (var (method, path, body) in requests)
    {
      using var request = new HttpRequestMessage(
        method,
        $"{HttpConstants.V1.DeviceFileSystemEndpoint}/{path}");
      if (body is not null)
      {
        request.Content = JsonContent.Create(body);
      }

      using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

      var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
        TestContext.Current.CancellationToken);
      Assert.Equal("Invalid tenant id.", problem?.Title);
    }
  }

  [Fact]
  public async Task CreateDirectory_WhenAgentRefuses_ReturnsConflictWithTheAgentsReason()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-create-refused@test.local");
    harness.AgentClient
      .Setup(x => x.CreateDirectory(It.IsAny<CreateDirectoryHubDto>()))
      .ReturnsAsync(HubResult.Fail("the parent path is read-only"));

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      harness.Tenant.Id,
      new CreateDeviceDirectoryRequestDto("/parent", "new-dir"),
      TestContext.Current.CancellationToken);

    var problem = AssertDeviceRefusal(result);
    Assert.Equal("the parent path is read-only", problem.Detail);
  }

  /// <summary>
  /// The caller holds every device file-system permission except the one this action applies, so a
  /// refusal can only come from the <c>FileSystemWrite</c> policy. Pointing the action at a sibling
  /// policy would leave the permission it needs present and this pin green.
  /// </summary>
  [Fact]
  public async Task CreateDirectory_WhenCallerLacksFileSystemWrite_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateWithDeviceAccessLackingAsync(
      scope,
      "v1-dfs-create-no-perm@test.local",
      PermissionNames.DeviceFileSystemWrite);

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      harness.Tenant.Id,
      new CreateDeviceDirectoryRequestDto("/parent", "new-dir"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task CreateDirectory_WhenDeviceDoesNotExist_ReturnsNotFoundWithoutReachingTheAgent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-create-notfound@test.local");

    var result = await harness.Controller.CreateDirectory(
      Guid.NewGuid(),
      harness.Tenant.Id,
      new CreateDeviceDirectoryRequestDto("/parent", "new-dir"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task CreateDirectory_WhenDeviceIsOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-create-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      harness.Tenant.Id,
      new CreateDeviceDirectoryRequestDto("/parent", "new-dir"),
      TestContext.Current.CancellationToken);

    AssertDeviceOfflineConflict(result);
  }

  [Fact]
  public async Task CreateDirectory_WhenDirectoryNameIsMissing_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-create-missing@test.local");

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      harness.Tenant.Id,
      new CreateDeviceDirectoryRequestDto("/parent", " "),
      TestContext.Current.CancellationToken);

    AssertBadRequest(harness, result);
  }

  [Fact]
  public async Task CreateDirectory_WhenServerPrincipalNamesAnotherTenantsDevice_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-create-cross-tenant@test.local");
    var otherTenant = await harness.Services.CreateTestTenant("V1 DFS Other Tenant");
    await harness.UseServerPrincipal("v1-dfs-create-cross-tenant-sa");
    harness.AgentClient
      .Setup(x => x.CreateDirectory(It.IsAny<CreateDirectoryHubDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      otherTenant.Id,
      new CreateDeviceDirectoryRequestDto("/parent", "new-dir"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task CreateDirectory_WhenTheCallerNamesAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-create-foreign@test.local");
    var foreignTenant = await harness.Services.CreateTestTenant("V1 DFS Create Foreign");

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      foreignTenant.Id,
      new CreateDeviceDirectoryRequestDto("/parent", "new-dir"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task CreateDirectory_WhenTheRequestIsUsable_ReturnsNoContentAndForwardsTheHubDto()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-create-success@test.local");
    harness.AgentClient
      .Setup(x => x.CreateDirectory(It.IsAny<CreateDirectoryHubDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      harness.Tenant.Id,
      new CreateDeviceDirectoryRequestDto("/parent", "new-dir"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NoContentResult>(result);
    Assert.Equal([OnlineConnectionId], harness.ConnectionIds);
    harness.AgentClient.Verify(
      x => x.CreateDirectory(It.Is<CreateDirectoryHubDto>(
        dto => dto.ParentPath == "/parent" && dto.DirectoryName == "new-dir")),
      Times.Once());
  }

  [Fact]
  public async Task DeletePath_WhenAgentRefuses_ReturnsConflictWithTheAgentsReason()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-delete-refused@test.local");
    harness.AgentClient
      .Setup(x => x.DeleteFile(It.IsAny<FileDeleteHubDto>()))
      .ReturnsAsync(HubResult.Fail("the file is in use"));

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      harness.Tenant.Id,
      new DeleteDevicePathRequestDto("/parent/file.txt"),
      TestContext.Current.CancellationToken);

    var problem = AssertDeviceRefusal(result);
    Assert.Equal("the file is in use", problem.Detail);
  }

  /// <summary>
  /// The caller holds every device file-system permission except the one this action applies, so a
  /// refusal can only come from the <c>FileSystemDelete</c> policy.
  /// </summary>
  [Fact]
  public async Task DeletePath_WhenCallerLacksFileSystemDelete_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateWithDeviceAccessLackingAsync(
      scope,
      "v1-dfs-delete-no-perm@test.local",
      PermissionNames.DeviceFileSystemDelete);

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      harness.Tenant.Id,
      new DeleteDevicePathRequestDto("/parent/file.txt"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task DeletePath_WhenDeviceDoesNotExist_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-delete-notfound@test.local");

    var result = await harness.Controller.DeletePath(
      Guid.NewGuid(),
      harness.Tenant.Id,
      new DeleteDevicePathRequestDto("/parent/file.txt"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task DeletePath_WhenDeviceIsOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-delete-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      harness.Tenant.Id,
      new DeleteDevicePathRequestDto("/parent/file.txt"),
      TestContext.Current.CancellationToken);

    AssertDeviceOfflineConflict(result);
  }

  [Fact]
  public async Task DeletePath_WhenFilePathIsMissing_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-delete-missing@test.local");

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      harness.Tenant.Id,
      new DeleteDevicePathRequestDto(""),
      TestContext.Current.CancellationToken);

    AssertBadRequest(harness, result);
  }

  [Fact]
  public async Task DeletePath_WhenServerPrincipalNamesAnotherTenantsDevice_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-delete-cross-tenant@test.local");
    var otherTenant = await harness.Services.CreateTestTenant("V1 DFS Other Tenant");
    await harness.UseServerPrincipal("v1-dfs-delete-cross-tenant-sa");
    harness.AgentClient
      .Setup(x => x.DeleteFile(It.IsAny<FileDeleteHubDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      otherTenant.Id,
      new DeleteDevicePathRequestDto("/parent/file.txt"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task DeletePath_WhenTheCallerNamesAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-delete-foreign@test.local");
    var foreignTenant = await harness.Services.CreateTestTenant("V1 DFS Delete Foreign");

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      foreignTenant.Id,
      new DeleteDevicePathRequestDto("/parent/file.txt"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task DeletePath_WhenTheRequestIsUsable_ReturnsTheNamedResponseEnvelope()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-delete-success@test.local");
    harness.AgentClient
      .Setup(x => x.DeleteFile(It.IsAny<FileDeleteHubDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      harness.Tenant.Id,
      new DeleteDevicePathRequestDto("/parent/file.txt"),
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<DevicePathDeletionResponseDto>(ok.Value);
    Assert.Equal("/parent/file.txt", response.FilePath);
    Assert.Equal("File deletion completed", response.Message);
    harness.AgentClient.Verify(
      x => x.DeleteFile(It.Is<FileDeleteHubDto>(dto => dto.TargetPath == "/parent/file.txt")),
      Times.Once());
  }

  [Fact]
  public async Task GetDirectoryContents_WhenAgentRefusesTheStream_ReturnsConflictWithTheAgentsReason()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-contents-refused@test.local");
    harness.AgentClient
      .Setup(x => x.StreamDirectoryContents(It.IsAny<DirectoryContentsStreamRequestHubDto>()))
      .ReturnsAsync(HubResult.Fail("the directory could not be enumerated"));

    var result = await harness.Controller.GetDirectoryContents(
      harness.Tenant.Id,
      new DeviceDirectoryContentsRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    var problem = AssertDeviceRefusal(result);
    Assert.Equal("the directory could not be enumerated", problem.Detail);
  }

  /// <summary>
  /// The caller holds every device file-system permission except the one this action applies, so a
  /// refusal can only come from the <c>FileSystemRead</c> policy.
  /// </summary>
  [Fact]
  public async Task GetDirectoryContents_WhenCallerLacksFileSystemRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateWithDeviceAccessLackingAsync(
      scope,
      "v1-dfs-contents-no-perm@test.local",
      PermissionNames.DeviceFileSystemRead);

    var result = await harness.Controller.GetDirectoryContents(
      harness.Tenant.Id,
      new DeviceDirectoryContentsRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetDirectoryContents_WhenDeviceDoesNotExist_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-contents-notfound@test.local");

    var result = await harness.Controller.GetDirectoryContents(
      harness.Tenant.Id,
      new DeviceDirectoryContentsRequestDto(Guid.NewGuid(), "/parent"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetDirectoryContents_WhenDeviceIsOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-contents-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await harness.Controller.GetDirectoryContents(
      harness.Tenant.Id,
      new DeviceDirectoryContentsRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    AssertDeviceOfflineConflict(result);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenServerPrincipalNamesAnotherTenantsDevice_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-contents-cross-tenant@test.local");
    var otherTenant = await harness.Services.CreateTestTenant("V1 DFS Other Tenant");
    await harness.UseServerPrincipal("v1-dfs-contents-cross-tenant-sa");
    harness.AgentClient
      .Setup(x => x.StreamDirectoryContents(It.IsAny<DirectoryContentsStreamRequestHubDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await harness.Controller.GetDirectoryContents(
      otherTenant.Id,
      new DeviceDirectoryContentsRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetDirectoryContents_WhenStreamIsCanceled_ReturnsRequestTimeout()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-contents-canceled@test.local");

    // The agent's answer arrives before the drain, so the drain is the only thing left that can
    // observe the cancellation, which is what the 408 branch is reached through.
    using var drainCanceledCts = new CancellationTokenSource();
    using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(
      drainCanceledCts.Token,
      TestContext.Current.CancellationToken);
    harness.AgentClient
      .Setup(x => x.StreamDirectoryContents(It.IsAny<DirectoryContentsStreamRequestHubDto>()))
      .ReturnsAsync(() =>
      {
        drainCanceledCts.Cancel();
        return HubResult.Ok();
      });

    var result = await harness.Controller.GetDirectoryContents(
      harness.Tenant.Id,
      new DeviceDirectoryContentsRequestDto(harness.Device.Id, "/parent"),
      requestCts.Token);

    var timeoutResult = Assert.IsType<StatusCodeResult>(result);
    Assert.Equal(StatusCodes.Status408RequestTimeout, timeoutResult.StatusCode);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenStreamYieldsChunks_ReturnsV1EntriesAndDirectoryExists()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-contents-success@test.local");
    harness.AgentClient
      .Setup(x => x.StreamDirectoryContents(It.IsAny<DirectoryContentsStreamRequestHubDto>()))
      .ReturnsAsync((DirectoryContentsStreamRequestHubDto dto) =>
      {
        var signaler = harness.HubStreamStore.GetOrCreate<InternalDtos.FileSystemEntryDto[]>(dto.StreamId);
        signaler.Writer.TryWrite([CreateEntry("a.txt"), CreateEntry("b.txt")]);
        signaler.Writer.TryWrite([CreateEntry("sub", isDirectory: true)]);
        signaler.Metadata = true;
        signaler.SetWriteCompleted();
        return HubResult.Ok();
      });

    var result = await harness.Controller.GetDirectoryContents(
      harness.Tenant.Id,
      new DeviceDirectoryContentsRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<DeviceDirectoryContentsResponseDto>(ok.Value);
    Assert.True(response.DirectoryExists);
    Assert.Equal(["a.txt", "b.txt", "sub"], response.Items.Select(x => x.Name));
    Assert.Equal("/parent/sub", response.Items[2].FullPath);
    Assert.True(response.Items[2].HasSubfolders);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenTheCallerNamesAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-contents-foreign@test.local");
    var foreignTenant = await harness.Services.CreateTestTenant("V1 DFS Contents Foreign");

    var result = await harness.Controller.GetDirectoryContents(
      foreignTenant.Id,
      new DeviceDirectoryContentsRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task GetLogFiles_WhenAgentRefuses_ReturnsConflictWithTheAgentsReason()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-logs-refused@test.local");
    harness.AgentClient
      .Setup(x => x.GetLogFiles())
      .ReturnsAsync(HubResult.Fail<InternalDtos.GetLogFilesResponseDto>("agent log scan failed"));

    var result = await harness.Controller.GetLogFiles(
      harness.Device.Id,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    var problem = AssertDeviceRefusal(result);
    Assert.Equal("agent log scan failed", problem.Detail);
  }

  /// <summary>
  /// The caller holds every device file-system permission except the one this action applies, so a
  /// refusal can only come from the <c>LogsRead</c> policy.
  /// </summary>
  [Fact]
  public async Task GetLogFiles_WhenCallerLacksLogsRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateWithDeviceAccessLackingAsync(
      scope,
      "v1-dfs-logs-no-perm@test.local",
      PermissionNames.DeviceLogsRead);

    var result = await harness.Controller.GetLogFiles(
      harness.Device.Id,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetLogFiles_WhenDeviceDoesNotExist_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-logs-notfound@test.local");

    var result = await harness.Controller.GetLogFiles(
      Guid.NewGuid(),
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetLogFiles_WhenDeviceIsOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-logs-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await harness.Controller.GetLogFiles(
      harness.Device.Id,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    AssertDeviceOfflineConflict(result);
  }

  [Fact]
  public async Task GetLogFiles_WhenServerPrincipalNamesAnotherTenantsDevice_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-logs-cross-tenant@test.local");
    var otherTenant = await harness.Services.CreateTestTenant("V1 DFS Other Tenant");
    await harness.UseServerPrincipal("v1-dfs-logs-cross-tenant-sa");
    ArmLogFiles(harness, []);

    // A server principal is trusted with the tenant it names, and its context carries no claims-driven
    // filter to fall back on, so the explicit tenant predicate is the only thing that can keep it off
    // another tenant's device.
    var result = await harness.Controller.GetLogFiles(
      harness.Device.Id,
      otherTenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetLogFiles_WhenServerPrincipalNamesTheDeviceTenant_ReachesTheAgent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-logs-server-same-tenant@test.local");
    await harness.UseServerPrincipal("v1-dfs-logs-same-tenant-sa");
    ArmLogFiles(harness, [
      new InternalDtos.LogFileGroupDto(
        "Agent",
        [new InternalDtos.LogFileEntryDto("agent.log", "/logs/agent.log", 123, DateTimeOffset.UnixEpoch)]),
    ]);

    // The positive control for the predicate above: naming the device's own tenant must not 404.
    var result = await harness.Controller.GetLogFiles(
      harness.Device.Id,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result);
    Assert.IsType<DeviceLogFileListResponseDto>(ok.Value);
    Assert.Equal([OnlineConnectionId], harness.ConnectionIds);
  }

  /// <summary>
  /// A tenant-bound caller names its own valid tenant but a device id from another tenant.
  /// </summary>
  [Fact]
  public async Task GetLogFiles_WhenTenantBoundCallerNamesItsOwnTenantForAForeignDevice_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-logs-own-tenant-foreign-device@test.local");
    var foreignTenant = await harness.Services.CreateTestTenant("V1 DFS Foreign Device Tenant");
    var foreignDevice = await harness.Services.CreateTestDevice(foreignTenant.Id);
    ArmLogFiles(harness, []);

    var result = await harness.Controller.GetLogFiles(
      foreignDevice.Id,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetLogFiles_WhenTheCallerNamesAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-logs-foreign@test.local");
    var foreignTenant = await harness.Services.CreateTestTenant("V1 DFS Logs Foreign");

    var result = await harness.Controller.GetLogFiles(
      harness.Device.Id,
      foreignTenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task GetLogFiles_WhenTheDeviceHasLogFiles_ReturnsTheGrouping()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-logs-success@test.local");
    ArmLogFiles(harness, [
      new InternalDtos.LogFileGroupDto(
        "Server",
        [
          new InternalDtos.LogFileEntryDto("server.log", "/logs/server.log", 4_567, DateTimeOffset.UnixEpoch),
          new InternalDtos.LogFileEntryDto("startup.log", "/logs/startup.log", 89, DateTimeOffset.UnixEpoch),
        ]),
    ]);

    var result = await harness.Controller.GetLogFiles(
      harness.Device.Id,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<DeviceLogFileListResponseDto>(ok.Value);
    var group = Assert.Single(response.LogFileGroups);
    Assert.Equal("Server", group.GroupName);
    Assert.Equal(["server.log", "startup.log"], group.LogFiles.Select(x => x.FileName));
    Assert.Equal(4_567L, group.LogFiles[0].Size);
  }

  /// <summary>
  /// The agent reported success but carried no payload, which no operation produces today.
  /// </summary>
  [Fact]
  public async Task GetLogFiles_WhenTheSuccessCarriesNoPayload_ReturnsInternalServerError()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-logs-no-payload@test.local");
    harness.AgentClient
      .Setup(x => x.GetLogFiles())
      .ReturnsAsync(HubResult.Ok<InternalDtos.GetLogFilesResponseDto>(null!));

    var result = await harness.Controller.GetLogFiles(
      harness.Device.Id,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal("The remote device returned an unexpected response.", problem.Title);
  }

  [Fact]
  public async Task GetPathSegments_WhenAgentNeverAnswers_ReturnsBadGateway()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-segments-noanswer@test.local");
    InternalDtos.PathSegmentsResponseDto? noResponse = null;
    harness.AgentClient
      .Setup(x => x.GetPathSegments(It.IsAny<GetPathSegmentsHubDto>()))
      .ReturnsAsync(noResponse!);

    var result = await harness.Controller.GetPathSegments(
      harness.Tenant.Id,
      new DevicePathSegmentsRequestDto(harness.Device.Id, "/parent/child"),
      TestContext.Current.CancellationToken);

    // Nothing answered, so there is no reason to report and this is the one case that stays a 502.
    // The deprecated endpoint called it a 500.
    var problem = AssertNoAnswerFromDevice(result);
    Assert.Equal("The device did not return a result.", problem.Detail);
  }

  /// <summary>
  /// The caller holds every device file-system permission except the one this action applies, so a
  /// refusal can only come from the <c>FileSystemRead</c> policy.
  /// </summary>
  [Fact]
  public async Task GetPathSegments_WhenCallerLacksFileSystemRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateWithDeviceAccessLackingAsync(
      scope,
      "v1-dfs-segments-no-perm@test.local",
      PermissionNames.DeviceFileSystemRead);

    var result = await harness.Controller.GetPathSegments(
      harness.Tenant.Id,
      new DevicePathSegmentsRequestDto(harness.Device.Id, "/parent/child"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  /// <summary>
  /// The one operation whose deprecated endpoint called a missing device a 400. The versioned surface
  /// refuses to inherit that.
  /// </summary>
  [Fact]
  public async Task GetPathSegments_WhenDeviceDoesNotExist_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-segments-notfound@test.local");

    var result = await harness.Controller.GetPathSegments(
      harness.Tenant.Id,
      new DevicePathSegmentsRequestDto(Guid.NewGuid(), "/parent/child"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetPathSegments_WhenDeviceIsOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-segments-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await harness.Controller.GetPathSegments(
      harness.Tenant.Id,
      new DevicePathSegmentsRequestDto(harness.Device.Id, "/parent/child"),
      TestContext.Current.CancellationToken);

    AssertDeviceOfflineConflict(result);
  }

  [Fact]
  public async Task GetPathSegments_WhenServerPrincipalNamesAnotherTenantsDevice_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-segments-cross-tenant@test.local");
    var otherTenant = await harness.Services.CreateTestTenant("V1 DFS Other Tenant");
    await harness.UseServerPrincipal("v1-dfs-segments-cross-tenant-sa");
    harness.AgentClient
      .Setup(x => x.GetPathSegments(It.IsAny<GetPathSegmentsHubDto>()))
      .ReturnsAsync(new InternalDtos.PathSegmentsResponseDto
      {
        PathExists = true,
        PathSegments = ["parent", "child"],
        Success = true,
      });

    // Path segments keeps its guards inline rather than in the shared Guard helper, but the tenant
    // predicate it applies is the same LoadDevice every other action uses. Only the surrounding guard
    // differs, which is why this pin is written against the shared predicate rather than a parallel one.
    var result = await harness.Controller.GetPathSegments(
      otherTenant.Id,
      new DevicePathSegmentsRequestDto(harness.Device.Id, "/parent/child"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetPathSegments_WhenTheCallerNamesAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-segments-foreign@test.local");
    var foreignTenant = await harness.Services.CreateTestTenant("V1 DFS Segments Foreign");

    var result = await harness.Controller.GetPathSegments(
      foreignTenant.Id,
      new DevicePathSegmentsRequestDto(harness.Device.Id, "/parent/child"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task GetPathSegments_WhenTheDeviceAnswers_ReturnsTheSegments()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-segments-success@test.local");
    harness.AgentClient
      .Setup(x => x.GetPathSegments(It.IsAny<GetPathSegmentsHubDto>()))
      .ReturnsAsync(new InternalDtos.PathSegmentsResponseDto
      {
        ErrorMessage = "",
        PathExists = true,
        PathSegments = ["parent", "child"],
        PathSeparator = "/",
        Success = true,
      });

    var result = await harness.Controller.GetPathSegments(
      harness.Tenant.Id,
      new DevicePathSegmentsRequestDto(harness.Device.Id, "/parent/child"),
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<DevicePathSegmentsResponseDto>(ok.Value);
    Assert.Equal(["parent", "child"], response.PathSegments);
    Assert.Equal("/", response.PathSeparator);
    Assert.True(response.PathExists);
    Assert.True(response.Success);
  }

  [Fact]
  public async Task GetRootDrives_WhenAgentRefuses_ReturnsConflictWithTheAgentsReason()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-drives-refused@test.local");
    harness.AgentClient
      .Setup(x => x.GetRootDrives(It.IsAny<InternalDtos.GetRootDrivesRequestDto>()))
      .ReturnsAsync(HubResult.Fail<InternalDtos.GetRootDrivesResponseDto>("no roots enumerated"));

    var result = await harness.Controller.GetRootDrives(
      harness.Tenant.Id,
      new DeviceRootDrivesRequestDto(harness.Device.Id),
      TestContext.Current.CancellationToken);

    var problem = AssertDeviceRefusal(result);
    Assert.Equal("no roots enumerated", problem.Detail);
  }

  /// <summary>
  /// The caller holds every device file-system permission except the one this action applies, so a
  /// refusal can only come from the <c>FileSystemRead</c> policy.
  /// </summary>
  [Fact]
  public async Task GetRootDrives_WhenCallerLacksFileSystemRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateWithDeviceAccessLackingAsync(
      scope,
      "v1-dfs-drives-no-perm@test.local",
      PermissionNames.DeviceFileSystemRead);

    var result = await harness.Controller.GetRootDrives(
      harness.Tenant.Id,
      new DeviceRootDrivesRequestDto(harness.Device.Id),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetRootDrives_WhenDeviceDoesNotExist_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-drives-notfound@test.local");

    var result = await harness.Controller.GetRootDrives(
      harness.Tenant.Id,
      new DeviceRootDrivesRequestDto(Guid.NewGuid()),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetRootDrives_WhenDeviceIsOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-drives-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await harness.Controller.GetRootDrives(
      harness.Tenant.Id,
      new DeviceRootDrivesRequestDto(harness.Device.Id),
      TestContext.Current.CancellationToken);

    AssertDeviceOfflineConflict(result);
  }

  [Fact]
  public async Task GetRootDrives_WhenServerPrincipalNamesAnotherTenantsDevice_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-drives-cross-tenant@test.local");
    var otherTenant = await harness.Services.CreateTestTenant("V1 DFS Other Tenant");
    await harness.UseServerPrincipal("v1-dfs-drives-cross-tenant-sa");
    harness.AgentClient
      .Setup(x => x.GetRootDrives(It.IsAny<InternalDtos.GetRootDrivesRequestDto>()))
      .ReturnsAsync(HubResult.Ok(new InternalDtos.GetRootDrivesResponseDto([])));

    var result = await harness.Controller.GetRootDrives(
      otherTenant.Id,
      new DeviceRootDrivesRequestDto(harness.Device.Id),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetRootDrives_WhenTheCallerNamesAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-drives-foreign@test.local");
    var foreignTenant = await harness.Services.CreateTestTenant("V1 DFS Drives Foreign");

    var result = await harness.Controller.GetRootDrives(
      foreignTenant.Id,
      new DeviceRootDrivesRequestDto(harness.Device.Id),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task GetRootDrives_WhenTheDeviceEnumeratesItsRoots_ReturnsTheEntries()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-drives-success@test.local");
    harness.AgentClient
      .Setup(x => x.GetRootDrives(It.IsAny<InternalDtos.GetRootDrivesRequestDto>()))
      .ReturnsAsync(HubResult.Ok(new InternalDtos.GetRootDrivesResponseDto([
        CreateEntry("C:", isDirectory: true),
        CreateEntry("D:", isDirectory: true),
      ])));

    var result = await harness.Controller.GetRootDrives(
      harness.Tenant.Id,
      new DeviceRootDrivesRequestDto(harness.Device.Id),
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<DeviceRootDrivesResponseDto>(ok.Value);
    Assert.Equal(["C:", "D:"], response.Drives.Select(x => x.Name));
    Assert.True(response.Drives[0].CanRead);
  }

  /// <summary>
  /// A server fault rather than a device fault, the one condition that reaches the declared 500.
  /// </summary>
  [Fact]
  public async Task GetRootDrives_WhenTheHubCallThrows_ReturnsInternalServerError()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-drives-throws@test.local");
    harness.AgentClient
      .Setup(x => x.GetRootDrives(It.IsAny<InternalDtos.GetRootDrivesRequestDto>()))
      .ThrowsAsync(new InvalidOperationException("hub boom"));

    var result = await harness.Controller.GetRootDrives(
      harness.Tenant.Id,
      new DeviceRootDrivesRequestDto(harness.Device.Id),
      TestContext.Current.CancellationToken);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal("Error contacting the remote device.", problem.Title);
  }

  [Fact]
  public async Task GetSubdirectories_WhenAgentRefusesTheStream_ReturnsConflictWithTheAgentsReason()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-subdirs-refused@test.local");
    harness.AgentClient
      .Setup(x => x.StreamSubdirectories(It.IsAny<SubdirectoriesStreamRequestHubDto>()))
      .ReturnsAsync(HubResult.Fail("agent could not enumerate subdirectories"));

    var result = await harness.Controller.GetSubdirectories(
      harness.Tenant.Id,
      new DeviceSubdirectoriesRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    var problem = AssertDeviceRefusal(result);
    Assert.Equal("agent could not enumerate subdirectories", problem.Detail);
  }

  /// <summary>
  /// The caller holds every device file-system permission except the one this action applies, so a
  /// refusal can only come from the <c>FileSystemRead</c> policy.
  /// </summary>
  [Fact]
  public async Task GetSubdirectories_WhenCallerLacksFileSystemRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateWithDeviceAccessLackingAsync(
      scope,
      "v1-dfs-subdirs-no-perm@test.local",
      PermissionNames.DeviceFileSystemRead);

    var result = await harness.Controller.GetSubdirectories(
      harness.Tenant.Id,
      new DeviceSubdirectoriesRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetSubdirectories_WhenDeviceDoesNotExist_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-subdirs-notfound@test.local");

    var result = await harness.Controller.GetSubdirectories(
      harness.Tenant.Id,
      new DeviceSubdirectoriesRequestDto(Guid.NewGuid(), "/parent"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetSubdirectories_WhenDeviceIsOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-subdirs-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await harness.Controller.GetSubdirectories(
      harness.Tenant.Id,
      new DeviceSubdirectoriesRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    AssertDeviceOfflineConflict(result);
  }

  [Fact]
  public async Task GetSubdirectories_WhenServerPrincipalNamesAnotherTenantsDevice_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-subdirs-cross-tenant@test.local");
    var otherTenant = await harness.Services.CreateTestTenant("V1 DFS Other Tenant");
    await harness.UseServerPrincipal("v1-dfs-subdirs-cross-tenant-sa");
    harness.AgentClient
      .Setup(x => x.StreamSubdirectories(It.IsAny<SubdirectoriesStreamRequestHubDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await harness.Controller.GetSubdirectories(
      otherTenant.Id,
      new DeviceSubdirectoriesRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetSubdirectories_WhenStreamYieldsChunks_ReturnsFlattenedSubdirectories()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-subdirs-success@test.local");
    harness.AgentClient
      .Setup(x => x.StreamSubdirectories(It.IsAny<SubdirectoriesStreamRequestHubDto>()))
      .ReturnsAsync((SubdirectoriesStreamRequestHubDto dto) =>
      {
        var signaler = harness.HubStreamStore.GetOrCreate<InternalDtos.FileSystemEntryDto[]>(dto.StreamId);
        signaler.Writer.TryWrite([CreateEntry("docs", isDirectory: true)]);
        signaler.Writer.TryWrite([CreateEntry("tmp", isDirectory: true)]);
        signaler.Metadata = true;
        signaler.SetWriteCompleted();
        return HubResult.Ok();
      });

    var result = await harness.Controller.GetSubdirectories(
      harness.Tenant.Id,
      new DeviceSubdirectoriesRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<DeviceSubdirectoriesResponseDto>(ok.Value);
    Assert.Equal(["docs", "tmp"], response.Subdirectories.Select(x => x.Name));
  }

  [Fact]
  public async Task GetSubdirectories_WhenTheCallerNamesAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-subdirs-foreign@test.local");
    var foreignTenant = await harness.Services.CreateTestTenant("V1 DFS Subdirs Foreign");

    var result = await harness.Controller.GetSubdirectories(
      foreignTenant.Id,
      new DeviceSubdirectoriesRequestDto(harness.Device.Id, "/parent"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  /// <summary>
  /// The agent's reply is the answer itself rather than a hub result, so an agent that never answered
  /// produces nothing.
  /// </summary>
  [Fact]
  public async Task ValidateFilePath_WhenAgentNeverAnswers_ReturnsBadGateway()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-validate-noanswer@test.local");
    InternalDtos.ValidateFilePathResponseDto? noResponse = null;
    harness.AgentClient
      .Setup(x => x.ValidateFilePath(It.IsAny<ValidateFilePathHubDto>()))
      .ReturnsAsync(noResponse!);

    var result = await harness.Controller.ValidateFilePath(
      harness.Device.Id,
      harness.Tenant.Id,
      new ValidateDeviceFilePathRequestDto("/parent", "file.txt"),
      TestContext.Current.CancellationToken);

    var problem = AssertNoAnswerFromDevice(result);
    Assert.Equal("The device did not return a result.", problem.Detail);
  }

  /// <summary>
  /// The caller holds every device file-system permission except the one this action applies, so a
  /// refusal can only come from the <c>FileSystemRead</c> policy.
  /// </summary>
  [Fact]
  public async Task ValidateFilePath_WhenCallerLacksFileSystemRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateWithDeviceAccessLackingAsync(
      scope,
      "v1-dfs-validate-no-perm@test.local",
      PermissionNames.DeviceFileSystemRead);

    var result = await harness.Controller.ValidateFilePath(
      harness.Device.Id,
      harness.Tenant.Id,
      new ValidateDeviceFilePathRequestDto("/parent", "file.txt"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task ValidateFilePath_WhenDeviceDoesNotExist_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-validate-notfound@test.local");

    var result = await harness.Controller.ValidateFilePath(
      Guid.NewGuid(),
      harness.Tenant.Id,
      new ValidateDeviceFilePathRequestDto("/parent", "file.txt"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task ValidateFilePath_WhenDeviceIsOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-validate-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await harness.Controller.ValidateFilePath(
      harness.Device.Id,
      harness.Tenant.Id,
      new ValidateDeviceFilePathRequestDto("/parent", "file.txt"),
      TestContext.Current.CancellationToken);

    AssertDeviceOfflineConflict(result);
  }

  [Fact]
  public async Task ValidateFilePath_WhenFileNameIsMissing_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-validate-missing@test.local");

    var result = await harness.Controller.ValidateFilePath(
      harness.Device.Id,
      harness.Tenant.Id,
      new ValidateDeviceFilePathRequestDto("/parent", ""),
      TestContext.Current.CancellationToken);

    AssertBadRequest(harness, result);
  }

  [Fact]
  public async Task ValidateFilePath_WhenServerPrincipalNamesAnotherTenantsDevice_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-validate-cross-tenant@test.local");
    var otherTenant = await harness.Services.CreateTestTenant("V1 DFS Other Tenant");
    await harness.UseServerPrincipal("v1-dfs-validate-cross-tenant-sa");
    harness.AgentClient
      .Setup(x => x.ValidateFilePath(It.IsAny<ValidateFilePathHubDto>()))
      .ReturnsAsync(new InternalDtos.ValidateFilePathResponseDto(true));

    var result = await harness.Controller.ValidateFilePath(
      harness.Device.Id,
      otherTenant.Id,
      new ValidateDeviceFilePathRequestDto("/parent", "file.txt"),
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task ValidateFilePath_WhenTheCallerNamesAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-validate-foreign@test.local");
    var foreignTenant = await harness.Services.CreateTestTenant("V1 DFS Validate Foreign");

    var result = await harness.Controller.ValidateFilePath(
      harness.Device.Id,
      foreignTenant.Id,
      new ValidateDeviceFilePathRequestDto("/parent", "file.txt"),
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task ValidateFilePath_WhenTheDeviceSaysThePathIsInvalid_AnswersOkWithTheAnswer()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-validate-invalid@test.local");
    harness.AgentClient
      .Setup(x => x.ValidateFilePath(It.IsAny<ValidateFilePathHubDto>()))
      .ReturnsAsync(new InternalDtos.ValidateFilePathResponseDto(false, "the file name has illegal characters"));

    var result = await harness.Controller.ValidateFilePath(
      harness.Device.Id,
      harness.Tenant.Id,
      new ValidateDeviceFilePathRequestDto("/parent", "bad|name"),
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<DeviceFilePathValidationResponseDto>(ok.Value);
    Assert.False(response.IsValid);
    Assert.Equal("the file name has illegal characters", response.ErrorMessage);
  }

  [Fact]
  public async Task ValidateFilePath_WhenTheDeviceSaysThePathIsUsable_AnswersOkWithTheAnswer()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-dfs-validate-success@test.local");
    harness.AgentClient
      .Setup(x => x.ValidateFilePath(It.IsAny<ValidateFilePathHubDto>()))
      .ReturnsAsync(new InternalDtos.ValidateFilePathResponseDto(true));

    var result = await harness.Controller.ValidateFilePath(
      harness.Device.Id,
      harness.Tenant.Id,
      new ValidateDeviceFilePathRequestDto("/parent", "file.txt"),
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<DeviceFilePathValidationResponseDto>(ok.Value);
    Assert.True(response.IsValid);
    Assert.Equal([OnlineConnectionId], harness.ConnectionIds);
  }

  private static void ArmLogFiles(Harness harness, IReadOnlyList<InternalDtos.LogFileGroupDto> groups)
  {
    harness.AgentClient
      .Setup(x => x.GetLogFiles())
      .ReturnsAsync(HubResult.Ok(new InternalDtos.GetLogFilesResponseDto(groups)));
  }

  private static void AssertBadRequest(Harness harness, IActionResult result)
  {
    var badRequest = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  /// <summary>
  /// The device is not connected, so the server cannot carry out the request.
  /// </summary>
  private static ProblemDetails AssertDeviceOfflineConflict(IActionResult result)
  {
    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    Assert.Equal(DeviceOfflineMessage, problem.Detail);
    return problem;
  }

  /// <summary>
  /// The device answered and refused.
  /// </summary>
  private static ProblemDetails AssertDeviceRefusal(IActionResult result)
  {
    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    Assert.Equal("The remote device could not complete the operation.", problem.Title);
    return problem;
  }

  /// <summary>
  /// The device produced no result at all, the only case this surface reports as a bad gateway.
  /// </summary>
  private static ProblemDetails AssertNoAnswerFromDevice(IActionResult result)
  {
    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal(StatusCodes.Status502BadGateway, problem.Status);
    Assert.Equal("No response from the remote device.", problem.Title);
    return problem;
  }

  private static async Task<HttpClient> CreateAuthenticatedClient(TestWebServer testServer, Guid tenantId)
  {
    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var user = await testServer.Services.CreateTestUser(tenantId, $"v1-dfs-{Guid.NewGuid():N}@t.local");
    var actor = new PrincipalDescriptor(PrincipalType.User, user.Id, tenantId, "test");
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        "V1 DFS PAT",
        PersonalAccessTokenPermissionMode.InheritOwner),
      user.Id,
      actor);
    Assert.True(patResult.IsSuccess);

    var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);
    return client;
  }

  private static InternalDtos.FileSystemEntryDto CreateEntry(string name, bool isDirectory = false) =>
    new(
      Name: name,
      FullPath: $"/parent/{name}",
      IsDirectory: isDirectory,
      Size: 42,
      LastModified: DateTimeOffset.UnixEpoch,
      IsHidden: false,
      CanRead: true,
      CanWrite: true,
      HasSubfolders: isDirectory);

  /// <summary>
  /// A controller wired to an authenticated caller and one online device in that caller's tenant.
  /// </summary>
  private sealed class Harness(
    DeviceFileSystemController controller,
    List<string> connectionIds,
    Device device,
    IServiceProvider services,
    Mock<IAgentHubClient> agentClient,
    Mock<IHubContext<AgentHub, IAgentHubClient>> agentHub,
    IHubStreamStore hubStreamStore,
    Tenant tenant,
    Guid callerId)
  {
    public Mock<IAgentHubClient> AgentClient { get; } = agentClient;

    public Mock<IHubContext<AgentHub, IAgentHubClient>> AgentHub { get; } = agentHub;

    public Guid CallerId { get; } = callerId;

    public List<string> ConnectionIds { get; } = connectionIds;

    public DeviceFileSystemController Controller { get; } = controller;

    public Device Device { get; } = device;

    public IHubStreamStore HubStreamStore { get; } = hubStreamStore;

    public IServiceProvider Services { get; } = services;

    public Tenant Tenant { get; } = tenant;

    public static async Task<Harness> CreateAsync(
      IServiceScope scope,
      string userEmail,
      params string[] presets)
    {
      var services = scope.ServiceProvider;
      string[] effectivePresets = presets.Length > 0 ? presets : [PermissionPresets.DeviceSuperUser];
      var (principalController, tenant, user) = await scope.CreateControllerWithTestData<DeviceFileSystemController>(
        userEmail: userEmail,
        presets: effectivePresets);
      var device = await services.CreateTestDevice(tenant.Id);
      var harness = Build(scope, principalController.ControllerContext, tenant, device, user.Id);

      await harness.SetDeviceOnline(isOnline: true);
      return harness;
    }

    /// <summary>
    /// A harness whose caller holds the device superuser preset with one permission revoked, so a denial
    /// can only come from the policy the action applies.
    /// </summary>
    public static async Task<Harness> CreateWithDeviceAccessLackingAsync(
      IServiceScope scope,
      string userEmail,
      string permissionName)
    {
      var harness = await CreateAsync(scope, userEmail, PermissionPresets.DeviceSuperUser);

      await using var db = harness.Services.GetRequiredService<AppDb>();
      var assignment = await db.PermissionAssignments.SingleAsync(
        x => x.PrincipalId == harness.CallerId && x.PermissionName == permissionName,
        TestContext.Current.CancellationToken);
      db.PermissionAssignments.Remove(assignment);
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);

      return harness;
    }

    public async Task SetDeviceOnline(bool isOnline)
    {
      await using var db = Services.GetRequiredService<AppDb>();
      var device = await db.Devices.FirstAsync(
        x => x.Id == Device.Id,
        TestContext.Current.CancellationToken);

      device.IsOnline = isOnline;
      device.ConnectionId = isOnline ? OnlineConnectionId : string.Empty;
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Swaps the caller for a server service account.
    /// </summary>
    public async Task UseServerPrincipal(string accountName)
    {
      var principal = await Services.CreateServerPrincipal(accountName);
      Controller.HttpContext.User = principal;
    }

    private static Harness Build(
      IServiceScope scope,
      ControllerContext callerContext,
      Tenant tenant,
      Device device,
      Guid callerId)
    {
      var services = scope.ServiceProvider;
      var connectionIds = new List<string>();
      var agentClient = new Mock<IAgentHubClient>();
      var agentHub = CreateAgentHubContext(agentClient, connectionIds);
      var hubStreamStore = services.GetRequiredService<IHubStreamStore>();
      var deviceFileSystem = new DeviceFileSystemService(
        services.GetRequiredService<AppDb>(),
        agentHub.Object,
        hubStreamStore,
        services.GetRequiredService<IAuthorizationService>(),
        services.GetRequiredService<ILogger<DeviceFileSystemService>>());

      // The caller context is reused rather than rebuilt, because it already carries the principal the
      // permission presets were assigned to.
      var controller = new DeviceFileSystemController(
        deviceFileSystem,
        services.GetRequiredService<ILogger<DeviceFileSystemController>>())
      {
        ControllerContext = callerContext,
      };

      return new Harness(
        controller,
        connectionIds,
        device,
        services,
        agentClient,
        agentHub,
        hubStreamStore,
        tenant,
        callerId);
    }

    private static Mock<IHubContext<AgentHub, IAgentHubClient>> CreateAgentHubContext(
      Mock<IAgentHubClient> agentClient,
      List<string> connectionIdLog)
    {
      var hubClients = new Mock<IHubClients<IAgentHubClient>>();
      hubClients
        .Setup(x => x.Client(It.IsAny<string>()))
        .Returns((string connectionId) =>
        {
          connectionIdLog.Add(connectionId);
          return agentClient.Object;
        });

      var agentHub = new Mock<IHubContext<AgentHub, IAgentHubClient>>();
      agentHub.Setup(x => x.Clients).Returns(hubClients.Object);
      return agentHub;
    }
  }
}
