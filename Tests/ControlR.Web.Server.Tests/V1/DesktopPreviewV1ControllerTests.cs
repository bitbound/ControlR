using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Api.V1;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Options;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;

namespace ControlR.Web.Server.Tests.V1;

/// <summary>
/// Desktop preview on the versioned controller. These assert the V1 contract: a required tenantId
/// resolved once per action, the resolved tenant applied to the device load as an explicit predicate,
/// and a problem document for every failure instead of the internal endpoint's bare statuses.
/// <para>
/// The harness <c>AppDb</c> has no <c>HttpContext</c>, so the claims-driven tenant filter is inactive
/// in every test here. The explicit predicate on the device load is what keeps a foreign device out,
/// and these tests pin it.
/// </para>
/// </summary>
public class DesktopPreviewV1ControllerTests(ITestOutputHelper testOutput)
{
  private const string OnlineConnectionId = "test-agent-connection-id";

  private readonly ITestOutputHelper _testOutput = testOutput;

  /// <summary>
  /// The convention matches the parameter by name and type, so a rename would silently drop the 400.
  /// </summary>
  [Fact]
  public void GetDesktopPreview_TakesARequiredTenantIdParameter()
  {
    var action = typeof(DesktopPreviewController)
      .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
      .Single(x => x.IsDefined(typeof(HttpMethodAttribute), inherit: false));

    var tenantId = Assert.Single(
      action.GetParameters(),
      x => x.Name == "tenantId" && x.ParameterType == typeof(Guid));

    Assert.False(tenantId.HasDefaultValue);
  }

  [Fact]
  public async Task GetDesktopPreview_WhenCallerLacksDesktopPreviewRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateLackingPermissionAsync(
      scope,
      "v1-preview-no-perm@test.local",
      PermissionNames.DeviceDesktopPreviewRead);

    var result = await harness.Controller.GetDesktopPreview(
      harness.Device.Id,
      42,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetDesktopPreview_WhenDesktopPreviewIsDisabled_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(
      _testOutput,
      new Dictionary<string, string?> { ["AppOptions:DisableDesktopPreview"] = "true" });
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-preview-disabled@test.local");

    var result = await harness.Controller.GetDesktopPreview(
      harness.Device.Id,
      42,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    AssertNotFound(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetDesktopPreview_WhenDeviceDoesNotExist_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-preview-notfound@test.local");

    var result = await harness.Controller.GetDesktopPreview(
      Guid.NewGuid(),
      42,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    AssertNotFound(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetDesktopPreview_WhenDeviceIsNotConnected_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-preview-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await harness.Controller.GetDesktopPreview(
      harness.Device.Id,
      42,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal("Conflict.", problem.Title);
    Assert.Equal("Device is currently offline.", problem.Detail);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  /// <summary>
  /// The device record still carries a connection id from before it dropped, so only the online flag
  /// says the device cannot serve this. Addressing that stale id would reach nobody.
  /// </summary>
  [Fact]
  public async Task GetDesktopPreview_WhenDeviceIsOfflineButKeepsAConnectionId_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-preview-stale-connection@test.local");
    await harness.SetDeviceOnline(isOnline: false, connectionId: OnlineConnectionId);

    var result = await harness.Controller.GetDesktopPreview(
      harness.Device.Id,
      42,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  /// <summary>
  /// The online flag was set but the connection id never landed, so the hub call the action would make
  /// addresses an empty id and silently returns nothing.
  /// </summary>
  [Fact]
  public async Task GetDesktopPreview_WhenDeviceIsOnlineWithoutAConnectionId_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-preview-missing-connection@test.local");
    await harness.SetDeviceOnline(isOnline: true, connectionId: string.Empty);

    var result = await harness.Controller.GetDesktopPreview(
      harness.Device.Id,
      42,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetDesktopPreview_WhenServerPrincipalNamesAnotherTenantsDevice_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-preview-cross-tenant@test.local");
    var otherTenant = await harness.Services.CreateTestTenant("V1 Preview Other Tenant");
    await harness.UseServerPrincipal("v1-preview-cross-tenant-sa");

    var result = await harness.Controller.GetDesktopPreview(
      harness.Device.Id,
      42,
      otherTenant.Id,
      TestContext.Current.CancellationToken);

    AssertNotFound(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetDesktopPreview_WhenTheAgentFails_ReturnsConflictWithTheAgentsReason()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-preview-failed@test.local");
    harness.AgentClient
      .Setup(x => x.RequestDesktopPreview(It.IsAny<DesktopPreviewRequestDto>()))
      .ReturnsAsync(HubResult.Fail("no interactive session is running"));

    var result = await harness.Controller.GetDesktopPreview(
      harness.Device.Id,
      42,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    AssertAgentRefusal(result, "no interactive session is running");
  }

  [Fact]
  public async Task GetDesktopPreview_WhenTheAgentNeverAnswers_ReturnsBadGateway()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-preview-noanswer@test.local");
    HubResult? noResponse = null;
    harness.AgentClient
      .Setup(x => x.RequestDesktopPreview(It.IsAny<DesktopPreviewRequestDto>()))
      .ReturnsAsync(noResponse!);

    var result = await harness.Controller.GetDesktopPreview(
      harness.Device.Id,
      42,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    AssertAgentNoResponse(result);
  }

  [Fact]
  public async Task GetDesktopPreview_WhenTheCallerNamesAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-preview-foreign@test.local");
    var foreignTenant = await harness.Services.CreateTestTenant("V1 Preview Foreign");

    var result = await harness.Controller.GetDesktopPreview(
      harness.Device.Id,
      42,
      foreignTenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetDesktopPreview_WhenTheProcessHasAPreview_StreamsTheImage()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput, recordHubStreamSessions: true);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "v1-preview-success@test.local");
    var body = harness.CaptureResponseBody();
    DesktopPreviewRequestDto? forwarded = null;
    harness.AgentClient
      .Setup(x => x.RequestDesktopPreview(It.IsAny<DesktopPreviewRequestDto>()))
      .ReturnsAsync((DesktopPreviewRequestDto dto) =>
      {
        forwarded = dto;
        var signaler = harness.HubStreamStore.GetOrCreate<byte[]>(
          dto.StreamId,
          HubStreamExpiration.DesktopPreview);

        signaler.Writer.TryWrite([0xFF, 0xD8, 0xFF]);
        signaler.SetWriteCompleted();
        return HubResult.Ok();
      });

    var result = await harness.Controller.GetDesktopPreview(
      harness.Device.Id,
      42,
      harness.Tenant.Id,
      TestContext.Current.CancellationToken);

    Assert.IsType<EmptyResult>(result);
    Assert.Equal<byte[]>([0xFF, 0xD8, 0xFF], body.ToArray());
    Assert.Equal("image/jpeg", harness.Controller.Response.ContentType);
    AssertPreviewLifetime(harness);
    Assert.Equal([OnlineConnectionId], harness.ConnectionIds);

    var dto = Assert.IsType<DesktopPreviewRequestDto>(forwarded);
    Assert.Equal(42, dto.TargetProcessId);
    Assert.NotEqual(Guid.Empty, dto.RequesterId);
  }

  /// <summary>
  /// The hub call answered with nothing, so the action reports 502 upstream-unreachable, matching
  /// <c>MapFailure</c>'s <c>NoResponse</c> mapping. The server itself is fine, so 503 would be the
  /// wrong status.
  /// </summary>
  private static ProblemDetails AssertAgentNoResponse(IActionResult result)
  {
    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal(StatusCodes.Status502BadGateway, problem.Status);
    Assert.Equal("Bad gateway.", problem.Title);
    Assert.Equal("The device did not return a result.", problem.Detail);
    return problem;
  }

  /// <summary>
  /// The hub call answered with a refusal, so the action reports the agent's reason at 409 to match
  /// <c>MapFailure</c>'s <c>RemoteFailure</c> mapping. 503 with a retry-style detail would send the
  /// caller back into the same refusal.
  /// </summary>
  private static ProblemDetails AssertAgentRefusal(IActionResult result, string expectedDetail)
  {
    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    Assert.Equal("Conflict.", problem.Title);
    Assert.Equal(expectedDetail, problem.Detail);
    return problem;
  }

  private static ProblemDetails AssertNotFound(IActionResult result)
  {
    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    Assert.Equal("Not found.", problem.Title);
    return problem;
  }

  /// <summary>
  /// A preview session lives as long as a preview, so the call site states that lifetime rather than
  /// taking whatever the store's default would be.
  /// </summary>
  private static void AssertPreviewLifetime(Harness harness)
  {
    var recorder = Assert.IsType<RecordingHubStreamStore>(harness.HubStreamStore);
    Assert.Equal([HubStreamExpiration.DesktopPreview], recorder.CreatedSessions.Select(x => x.Expiration));
  }

  /// <summary>
  /// A controller wired to an authenticated caller and one online device in that caller's tenant.
  /// </summary>
  private sealed class Harness(
    DesktopPreviewController controller,
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

    public DesktopPreviewController Controller { get; } = controller;

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
      var (principalController, tenant, user) = await scope.CreateControllerWithTestData<DesktopPreviewController>(
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
    public static async Task<Harness> CreateLackingPermissionAsync(
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

    /// <summary>
    /// Gives the response a body the test can read, because a bare DefaultHttpContext discards what a
    /// streaming action writes.
    /// </summary>
    public MemoryStream CaptureResponseBody()
    {
      var body = new MemoryStream();
      Controller.HttpContext.Response.Body = body;
      return body;
    }

    public async Task SetDeviceOnline(bool isOnline, string? connectionId = null)
    {
      await using var db = Services.GetRequiredService<AppDb>();
      var device = await db.Devices.FirstAsync(
        x => x.Id == Device.Id,
        TestContext.Current.CancellationToken);

      device.IsOnline = isOnline;
      device.ConnectionId = connectionId ?? (isOnline ? OnlineConnectionId : string.Empty);
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

      // The caller context is reused rather than rebuilt, because it already carries the principal the
      // permission presets were assigned to.
      var controller = new DesktopPreviewController(
        services.GetRequiredService<AppDb>(),
        agentHub.Object,
        services.GetRequiredService<IHubStreamStore>(),
        services.GetRequiredService<IAuthorizationService>(),
        services.GetRequiredService<IOptionsMonitor<AppOptions>>(),
        services.GetRequiredService<ILogger<DesktopPreviewController>>())
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
        services.GetRequiredService<IHubStreamStore>(),
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
