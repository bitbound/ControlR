using System.Security.Claims;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Options;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Settings;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ControlR.Web.Server.Services.Authorization.Capabilities;

namespace ControlR.Web.Server.Tests;

public class ViewerHubPermissionTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task AddViewerActivity2_Invoked_ReturnsHubResult()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var device = await testApp.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, user.Id, device.Id, tenant.Id, PermissionNames.DeviceRead);

    var (hub, _) = CreateHub(testApp, user, tenant.Id);

    var result = await hub.AddViewerActivity2(new("test-activity"));

    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task CloseTerminalSession2_AllowedDevice_ReturnsSuccess()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var device = await testApp.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, user.Id, device.Id, tenant.Id, PermissionNames.DeviceTerminalUse);

    var (hub, agentClient) = CreateHub(testApp, user, tenant.Id);
    agentClient
      .Setup(client => client.CloseTerminalSession(It.IsAny<Guid>()))
      .Returns(Task.CompletedTask);

    var result = await hub.CloseTerminalSession2(new(device.Id, Guid.NewGuid()));

    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task GetActiveDesktopSessions2_UnauthorizedDevice_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var device = await testApp.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, user.Id, device.Id, tenant.Id, PermissionNames.DeviceRead);

    var (hub, _) = CreateHub(testApp, user, tenant.Id);

    var result = await hub.GetActiveDesktopSessions2(new(device.Id));

    Assert.False(result.IsSuccess);
    Assert.Equal("Unauthorized.", result.Reason);
  }

  [Fact]
  public async Task GetDeviceAccessPermissions2_DeviceReadDenied_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var device = await testApp.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, user.Id, device.Id, tenant.Id, PermissionNames.DeviceTerminalUse);

    var (hub, _) = CreateHub(testApp, user, tenant.Id);

    var result = await hub.GetDeviceAccessPermissions2(new(device.Id));

    Assert.False(result.IsSuccess);
    Assert.Equal("Unauthorized.", result.Reason);
  }

  [Fact]
  public async Task RequestRemoteControlSession_AllowedDevice_ForwardsDtoDeviceIdentity()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var device = await testApp.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, user.Id, device.Id, tenant.Id, PermissionNames.DeviceRemoteControlConnect);
    var request = new RemoteControlSessionRequestDto(
      Guid.NewGuid(),
      new Uri("wss://localhost/remote-control"),
      1,
      100,
      device.Id,
      false,
      false);

    var (hub, agentClient) = CreateHub(testApp, user, tenant.Id);
    agentClient
      .Setup(client => client.CreateRemoteControlSession(It.IsAny<RemoteControlSessionRequestDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await hub.RequestRemoteControlSession2(request);

    Assert.True(result.IsSuccess);
    agentClient.Verify(client => client.CreateRemoteControlSession(
      It.Is<RemoteControlSessionRequestDto>(forwarded =>
        forwarded.DeviceId == request.DeviceId &&
        forwarded.ViewerConnectionId == hub.Context.ConnectionId)), Times.Once);
  }

  [Fact]
  public async Task RequestRemoteControlSession_CompatibilityOverload_ForwardsDtoDeviceIdentity()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var device = await testApp.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, user.Id, device.Id, tenant.Id, PermissionNames.DeviceRemoteControlConnect);
    var request = new RemoteControlSessionRequestDto(
      Guid.NewGuid(),
      new Uri("wss://localhost/remote-control"),
      1,
      100,
      Guid.Empty,
      false,
      false);

    var (hub, agentClient) = CreateHub(testApp, user, tenant.Id);
    agentClient
      .Setup(client => client.CreateRemoteControlSession(It.IsAny<RemoteControlSessionRequestDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await hub.RequestRemoteControlSession(device.Id, request);

    Assert.True(result.IsSuccess);
    agentClient.Verify(client => client.CreateRemoteControlSession(
      It.Is<RemoteControlSessionRequestDto>(forwarded =>
        forwarded.DeviceId == device.Id &&
        forwarded.ViewerConnectionId == hub.Context.ConnectionId)), Times.Once);
  }

  [Fact]
  public async Task RequestRemoteControlSession_GrantForDifferentDevice_DoesNotForward()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var allowedDevice = await testApp.Services.CreateTestDevice(tenant.Id);
    var requestedDevice = await testApp.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, user.Id, allowedDevice.Id, tenant.Id, PermissionNames.DeviceRemoteControlConnect);
    var request = new RemoteControlSessionRequestDto(
      Guid.NewGuid(),
      new Uri("wss://localhost/remote-control"),
      1,
      100,
      requestedDevice.Id,
      false,
      false);

    var (hub, agentClient) = CreateHub(testApp, user, tenant.Id);

    var result = await hub.RequestRemoteControlSession2(request);

    Assert.False(result.IsSuccess);
    agentClient.Verify(
      client => client.CreateRemoteControlSession(It.IsAny<RemoteControlSessionRequestDto>()),
      Times.Never);
  }

  [Fact]
  public async Task RequestVncSession_AllowedDevice_ForwardsDtoDeviceIdentity()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var device = await testApp.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, user.Id, device.Id, tenant.Id, PermissionNames.DeviceVncRelayConnect);
    var request = new VncSessionRequestDto(
      Guid.NewGuid(),
      new Uri("wss://localhost/vnc"),
      string.Empty,
      device.Id,
      false,
      5900);

    var (hub, agentClient) = CreateHub(testApp, user, tenant.Id);
    agentClient
      .Setup(client => client.CreateVncSession(It.IsAny<VncSessionRequestDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await hub.RequestVncSession2(request);

    Assert.True(result.IsSuccess);
    agentClient.Verify(client => client.CreateVncSession(
      It.Is<VncSessionRequestDto>(forwarded =>
        forwarded.DeviceId == request.DeviceId &&
        forwarded.ViewerConnectionId == hub.Context.ConnectionId)), Times.Once);
  }

  [Fact]
  public async Task RequestVncSession_CompatibilityOverload_ForwardsDtoDeviceIdentity()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var device = await testApp.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, user.Id, device.Id, tenant.Id, PermissionNames.DeviceVncRelayConnect);
    var request = new VncSessionRequestDto(
      Guid.NewGuid(),
      new Uri("wss://localhost/vnc"),
      string.Empty,
      Guid.Empty,
      false,
      5900);

    var (hub, agentClient) = CreateHub(testApp, user, tenant.Id);
    agentClient
      .Setup(client => client.CreateVncSession(It.IsAny<VncSessionRequestDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await hub.RequestVncSession(device.Id, request);

    Assert.True(result.IsSuccess);
    agentClient.Verify(client => client.CreateVncSession(
      It.Is<VncSessionRequestDto>(forwarded =>
        forwarded.DeviceId == device.Id &&
        forwarded.ViewerConnectionId == hub.Context.ConnectionId)), Times.Once);
  }

  [Fact]
  public async Task SendChatMessage2_AllowedDevice_ForwardsMessage()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var device = await testApp.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, user.Id, device.Id, tenant.Id, PermissionNames.DeviceChatSend);

    var (hub, agentClient) = CreateHub(testApp, user, tenant.Id);
    agentClient
      .Setup(client => client.SendChatMessage(It.IsAny<ChatMessageHubDto>()))
      .ReturnsAsync(HubResult.Ok());

    var chatMessage = new ChatMessageHubDto(
      device.Id,
      Guid.NewGuid(),
      "hello",
      string.Empty,
      string.Empty,
      0,
      0,
      DateTimeOffset.Now);

    var result = await hub.SendChatMessage2(chatMessage);

    Assert.True(result.IsSuccess);
    agentClient.Verify(client => client.SendChatMessage(It.Is<ChatMessageHubDto>(message =>
      message.Message == "hello" &&
      message.DeviceId == device.Id &&
      message.ViewerConnectionId == hub.Context.ConnectionId)), Times.Once);
  }

  [Fact]
  public async Task SubscribeToDeviceHeartbeats2_OverBatchLimit_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);

    var (hub, _) = CreateHub(testApp, user, tenant.Id);

    var deviceIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid());
    var result = await hub.SubscribeToDeviceHeartbeats2(new([.. deviceIds]));

    Assert.False(result.IsSuccess);
    Assert.Contains("Too many device IDs", result.Reason);
  }

  [Fact]
  public async Task TestVncConnection2_UnauthorizedDevice_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var device = await testApp.Services.CreateTestDevice(tenant.Id);
    await SeedAssignment(testApp, user.Id, device.Id, tenant.Id, PermissionNames.DeviceRead);

    var (hub, agentClient) = CreateHub(testApp, user, tenant.Id);

    var result = await hub.TestVncConnection2(new(device.Id, 5900));

    Assert.False(result.IsSuccess);
    Assert.Equal("Unauthorized.", result.Reason);
    agentClient.Verify(
      client => client.TestVncConnection(It.IsAny<int>()),
      Times.Never);
  }

  private static (ViewerHub Hub, Mock<IAgentHubClient> AgentClient) CreateHub(
    TestApp testApp,
    AppUser user,
    Guid tenantId)
  {
    var scope = testApp.Services.CreateScope();
    var services = scope.ServiceProvider;
    var agentClient = new Mock<IAgentHubClient>();
    var agentClients = new Mock<IHubClients<IAgentHubClient>>();
    agentClients
      .Setup(clients => clients.Client(It.IsAny<string>()))
      .Returns(agentClient.Object);
    var agentHub = new Mock<IHubContext<AgentHub, IAgentHubClient>>();
    agentHub.SetupGet(context => context.Clients).Returns(agentClients.Object);
    var preferences = new Mock<IEffectiveUserPreferencesResolver>();
    preferences
      .Setup(resolver => resolver.GetNotifyUserOnSessionStart(
        It.IsAny<Guid>(),
        It.IsAny<Guid>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    var hub = new ViewerHub(
      services.GetRequiredService<TimeProvider>(),
      services.GetRequiredService<UserManager<AppUser>>(),
      services.GetRequiredService<AppDb>(),
      services.GetRequiredService<IAuthorizationService>(),
      services.GetRequiredService<IPermissionEvaluator>(),
      services.GetRequiredService<IResourceDescriptorFactory>(),
      agentHub.Object,
      preferences.Object,
      services.GetRequiredService<IHubStreamStore>(),
      services.GetRequiredService<IDesktopSessionAccessAuthorizer>(),
      services.GetRequiredService<IOptionsMonitor<AppOptions>>(),
      services.GetRequiredService<ILogger<ViewerHub>>());
    hub.Context = new TestHubCallerContext(CreatePrincipal(user, tenantId));
    return (hub, agentClient);
  }

  private static ClaimsPrincipal CreatePrincipal(AppUser user, Guid tenantId) =>
    new(new ClaimsIdentity(
    [
      new Claim(UserClaimTypes.UserId, user.Id.ToString()),
      new Claim(UserClaimTypes.TenantId, tenantId.ToString()),
      new Claim(UserClaimTypes.AuthenticationMethod, "test"),
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.User),
      new Claim(PrincipalClaimTypes.PrincipalId, user.Id.ToString()),
      new Claim(ClaimTypes.Name, user.UserName ?? "test-user")
    ], "TestAuth"));

  private static async Task SeedAssignment(
    TestApp testApp,
    Guid userId,
    Guid deviceId,
    Guid tenantId,
    string permissionName)
  {
    using var scope = testApp.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      userId,
      permissionName,
      PermissionScopeKind.Device,
      deviceId,
      tenantId,
      new PrincipalDescriptor(PrincipalType.User, userId, tenantId, "test")));
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  private sealed class TestHubCallerContext : HubCallerContext
  {
    private readonly CancellationTokenSource _connectionAborted = new();

    public TestHubCallerContext(ClaimsPrincipal user)
    {
      User = user;
      UserIdentifier = user.FindFirst(UserClaimTypes.UserId)?.Value;
    }

    public override CancellationToken ConnectionAborted => _connectionAborted.Token;
    public override string ConnectionId { get; } = Guid.NewGuid().ToString();
    public override IFeatureCollection Features { get; } = new FeatureCollection();
    public override IDictionary<object, object?> Items { get; } =
      new Dictionary<object, object?>();
    public override ClaimsPrincipal User { get; }
    public override string? UserIdentifier { get; }

    public override void Abort()
    {
      _connectionAborted.Cancel();
    }
  }
}
