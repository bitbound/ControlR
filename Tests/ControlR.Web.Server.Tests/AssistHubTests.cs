using System.Reflection;
using System.Security.Claims;
using System.Threading.RateLimiting;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Services.Assist;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ControlR.Web.Server.Tests;

public class AssistHubTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CancelAssistConsent_ByViewerConnectionWhilePending_ReturnsSessionToWaitingAndNotifiesAssist()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user: null, connectionId: "viewer-conn-1");

    var result = await hub.CancelAssistConsent(new(session.SessionId, await GetNonceAsync(cache, session.SessionId)));

    Assert.True(result.IsSuccess);
    Assert.Equal(AssistSessionStatus.WaitingForViewer, (await GetSessionAsync(cache, session.SessionId)).Status);
    clients.Assist.Verify(
      c => c.AssistConsentCancelled(It.Is<AssistConsentCancelledDto>(dto => dto.SessionId == session.SessionId)),
      Times.Once);
    clients.Viewer.Verify(
      c => c.AssistConsentCancelled(It.IsAny<AssistConsentCancelledDto>()),
      Times.Never);
  }

  [Fact]
  public async Task CancelAssistConsent_ByWrongConnection_ReturnsUnauthorizedAndLeavesSessionPending()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user: null, connectionId: "someone-else");

    var result = await hub.CancelAssistConsent(new(session.SessionId, await GetNonceAsync(cache, session.SessionId)));

    Assert.False(result.IsSuccess);
    Assert.Equal("Unauthorized.", result.Reason);
    Assert.Equal(AssistSessionStatus.PendingConsent, (await GetSessionAsync(cache, session.SessionId)).Status);
    clients.Assist.Verify(
      c => c.AssistConsentCancelled(It.IsAny<AssistConsentCancelledDto>()),
      Times.Never);
  }

  [Fact]
  public async Task CancelAssistConsent_WhenActive_ReturnsOkAndLeavesSessionActive()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    _ = await cache.TryActivateAsync(session.SessionId, (await GetSessionAsync(cache, session.SessionId)).ConsentNonce!.Value, TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user: null, connectionId: "viewer-conn-1");

    // The session is active, not pending, so the nonce is irrelevant here. Cancel just no-ops.
    var result = await hub.CancelAssistConsent(new(session.SessionId, Guid.Empty));

    Assert.True(result.IsSuccess);
    Assert.Equal(AssistSessionStatus.Active, (await GetSessionAsync(cache, session.SessionId)).Status);
    clients.Assist.Verify(
      c => c.AssistConsentCancelled(It.IsAny<AssistConsentCancelledDto>()),
      Times.Never);
  }

  [Fact]
  public async Task CreateAssistSession_NewConnection_ReturnsCodeAndSessionId()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var (hub, _) = await CreateHubAsync(testApp, user: null);

    var result = await hub.CreateAssistSession();

    Assert.True(result.IsSuccess);
    Assert.Equal(AssistSessionCache.CodeLength, result.Value!.Code.Length);
    Assert.NotEqual(Guid.Empty, result.Value.SessionId);
  }

  [Fact]
  public async Task CreateAssistSession_OverThePerHostLimit_IsRejected()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);

    // One permit per partition, so the second create on the same key has to be refused. The limiter
    // the app registers is far wider, and this test only needs to prove the hub honors a refusal.
    var limiter = PartitionedRateLimiter.Create<HubCallerContext, string>(
      _ => RateLimitPartition.GetFixedWindowLimiter(
        "same-host",
        _ => new FixedWindowRateLimiterOptions
        {
          PermitLimit = 1,
          Window = TimeSpan.FromMinutes(1),
          QueueLimit = 0,
          AutoReplenishment = false
        }));

    var (hub, _) = await CreateHubAsync(
      testApp,
      user: null,
      connectionId: "assist-conn-1",
      sessionCreateLimiter: limiter);

    Assert.True((await hub.CreateAssistSession()).IsSuccess);

    var rejected = await hub.CreateAssistSession();

    Assert.False(rejected.IsSuccess);
    Assert.Null(rejected.Value);
  }

  [Fact]
  public async Task EndAssistSession_ByAssistConnection_RemovesSessionAndNotifiesViewer()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    _ = await cache.TryActivateAsync(session.SessionId, (await GetSessionAsync(cache, session.SessionId)).ConsentNonce!.Value, TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user: null, connectionId: "assist-conn-1");

    var result = await hub.EndAssistSession(new(session.SessionId));

    Assert.True(result.IsSuccess);
    Assert.Null(await cache.TryGetAsync(session.SessionId, TestContext.Current.CancellationToken));
    clients.Viewer.Verify(
      c => c.AssistSessionEnded(It.Is<AssistSessionEndedDto>(dto =>
        dto.SessionId == session.SessionId && dto.Reason == AssistSessionEndedReason.EndedByPeer)),
      Times.Once);
  }

  [Fact]
  public async Task EndAssistSession_ByUnrelatedConnection_ReturnsUnauthorized()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    var (hub, _) = await CreateHubAsync(testApp, user: null, connectionId: "unrelated");

    var result = await hub.EndAssistSession(new(session.SessionId));

    Assert.False(result.IsSuccess);
    Assert.Equal("Unauthorized.", result.Reason);
    Assert.NotNull(await cache.TryGetAsync(session.SessionId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task JoinAssistSession_InvalidCodeFails_ReturnsFailure()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var cache = GetCache(testApp);
    _ = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    await GrantAssistConnectAsync(testApp, user, tenant.Id);
    var (hub, _) = await CreateHubAsync(testApp, user, tenant.Id);

    var result = await hub.JoinAssistSession(new("123456789"));

    Assert.False(result.IsSuccess);
    Assert.Equal("The code is invalid or no longer active.", result.Reason);
  }

  [Fact]
  public async Task JoinAssistSession_TooManyInvalidCodes_RefusesFurtherAttempts()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    await GrantAssistConnectAsync(testApp, user, tenant.Id);
    var (hub, _) = await CreateHubAsync(testApp, user, tenant.Id);

    HubResult<JoinAssistSessionResponseDto> lastResult = HubResult.Ok<JoinAssistSessionResponseDto>(new(Guid.Empty, Guid.Empty));
    for (var i = 0; i < 11; i++)
    {
      lastResult = await hub.JoinAssistSession(new("123456789"));
    }

    Assert.False(lastResult.IsSuccess);
    Assert.Contains("Too many invalid codes", lastResult.Reason);

    // Even the real code is refused once the limit is hit.
    var withRealCode = await hub.JoinAssistSession(new(session.Code));
    Assert.False(withRealCode.IsSuccess);
  }

  [Fact]
  public async Task JoinAssistSession_UnauthenticatedUser_ReturnsUnauthorized()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    _ = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    var (hub, _) = await CreateHubAsync(testApp, user: null);
    var session = await cache.TryGetByAssistConnectionIdAsync("assist-conn-1", TestContext.Current.CancellationToken);

    var result = await hub.JoinAssistSession(new(session!.Code));

    Assert.False(result.IsSuccess);
    Assert.Equal("Unauthorized.", result.Reason);
  }

  [Fact]
  public async Task JoinAssistSession_ValidCode_MovesToPendingConsentAndNotifiesAssist()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    var user = await testApp.Services.CreateTestUser(tenant.Id, email: "tech@example.com");
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    await GrantAssistConnectAsync(testApp, user, tenant.Id);
    var (hub, clients) = await CreateHubAsync(testApp, user, tenant.Id);

    var result = await hub.JoinAssistSession(new(session.Code));

    Assert.True(result.IsSuccess);
    Assert.Equal(session.SessionId, result.Value!.SessionId);
    Assert.Equal(AssistSessionStatus.PendingConsent, (await GetSessionAsync(cache, session.SessionId)).Status);

    var expectedNonce = await GetNonceAsync(cache, session.SessionId);
    Assert.Equal(expectedNonce, result.Value.ConsentNonce);
    clients.Assist.Verify(
      c => c.AssistConsentRequested(It.Is<AssistConsentRequestedDto>(dto =>
        dto.SessionId == session.SessionId &&
        dto.ViewerEmail == "tech@example.com" &&
        dto.ConsentNonce == expectedNonce)),
      Times.Once);
    clients.Viewer.Verify(
      c => c.AssistConsentRequested(It.IsAny<AssistConsentRequestedDto>()),
      Times.Never);
  }

  [Fact]
  public async Task JoinAssistSession_WithoutAssistConnectPermission_ReturnsFailureAndLeavesSessionWaiting()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var tenant = await testApp.Services.CreateTestTenant();
    // Seed a decoy user so the technician isn't auto-promoted to first-user server admin.
    _ = await testApp.Services.CreateTestUser(tenant.Id, email: "seed@t.local");
    var user = await testApp.Services.CreateTestUser(tenant.Id, email: "tech@example.com");
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user, tenant.Id);

    var result = await hub.JoinAssistSession(new(session.Code));

    Assert.False(result.IsSuccess);
    Assert.Contains("permission", result.Reason, StringComparison.OrdinalIgnoreCase);
    Assert.Equal(AssistSessionStatus.WaitingForViewer, (await GetSessionAsync(cache, session.SessionId)).Status);
    clients.Assist.Verify(
      c => c.AssistConsentRequested(It.IsAny<AssistConsentRequestedDto>()),
      Times.Never);
  }

  [Fact]
  public async Task OnDisconnected_AssistConnectionEnds_RemovesSessionAndNotifiesViewer()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user: null, connectionId: "assist-conn-1");

    await hub.OnDisconnectedAsync(exception: null);

    Assert.Null(await cache.TryGetAsync(session.SessionId, TestContext.Current.CancellationToken));
    clients.Viewer.Verify(
      c => c.AssistSessionEnded(It.Is<AssistSessionEndedDto>(dto =>
        dto.SessionId == session.SessionId && dto.Reason == AssistSessionEndedReason.PeerDisconnected)),
      Times.Once);
    clients.Assist.Verify(
      c => c.AssistSessionEnded(It.IsAny<AssistSessionEndedDto>()),
      Times.Never);
  }

  [Fact]
  public async Task OnDisconnected_ViewerConnectionEndsDuringConsent_ReturnsSessionToWaitingAndNotifiesAssist()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user: null, connectionId: "viewer-conn-1");

    await hub.OnDisconnectedAsync(exception: null);

    var updated = await GetSessionAsync(cache, session.SessionId);
    Assert.Equal(AssistSessionStatus.WaitingForViewer, updated.Status);
    Assert.Null(updated.ViewerConnectionId);
    clients.Assist.Verify(
      c => c.AssistConsentCancelled(It.Is<AssistConsentCancelledDto>(dto => dto.SessionId == session.SessionId)),
      Times.Once);
    clients.Viewer.Verify(
      c => c.AssistSessionEnded(It.IsAny<AssistSessionEndedDto>()),
      Times.Never);
  }

  [Fact]
  public async Task OnDisconnected_ViewerConnectionEndsWhileActive_RemovesSessionAndNotifiesAssist()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    _ = await cache.TryActivateAsync(session.SessionId, (await GetSessionAsync(cache, session.SessionId)).ConsentNonce!.Value, TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user: null, connectionId: "viewer-conn-1");

    await hub.OnDisconnectedAsync(exception: null);

    Assert.Null(await cache.TryGetAsync(session.SessionId, TestContext.Current.CancellationToken));
    clients.Assist.Verify(
      c => c.AssistSessionEnded(It.Is<AssistSessionEndedDto>(dto =>
        dto.SessionId == session.SessionId && dto.Reason == AssistSessionEndedReason.PeerDisconnected)),
      Times.Once);
    clients.Viewer.Verify(
      c => c.AssistSessionEnded(It.IsAny<AssistSessionEndedDto>()),
      Times.Never);
  }

  [Fact]
  public async Task RespondToAssistConsent_ApprovedFromAssistConnection_ActivatesAndSendsToken()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", "jane@example.com", TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user: null, connectionId: "assist-conn-1");

    var result = await hub.RespondToAssistConsent(new(session.SessionId, true, await GetNonceAsync(cache, session.SessionId)));

    Assert.True(result.IsSuccess);
    Assert.Equal(AssistSessionStatus.Active, (await GetSessionAsync(cache, session.SessionId)).Status);
    clients.Viewer.Verify(
      c => c.AssistConsentResponded(It.Is<AssistConsentRespondedDto>(dto =>
        dto.SessionId == session.SessionId && dto.Approved && dto.AccessToken == session.AccessToken)),
      Times.Once);
    clients.Assist.Verify(
      c => c.AssistSessionStarted(It.Is<AssistSessionStartedDto>(dto =>
        dto.SessionId == session.SessionId &&
        dto.ViewerEmail == "jane@example.com" &&
        dto.CommandPolicy != null &&
        dto.CommandPolicy.IsBoundTo(session.SessionId) &&
        dto.CommandPolicy.AllowedDtoTypes.Contains(RemoteControlViewerDtoType.KeyEvent) &&
        !dto.CommandPolicy.AllowedDtoTypes.Contains(RemoteControlViewerDtoType.ToggleBlockInput) &&
        !dto.CommandPolicy.AllowedDtoTypes.Contains(RemoteControlViewerDtoType.TogglePrivacyScreen))),
      Times.Once);
  }

  [Fact]
  public async Task RespondToAssistConsent_ApprovedWithStaleNonce_FailsAndLeavesSessionPending()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user: null, connectionId: "assist-conn-1");

    var result = await hub.RespondToAssistConsent(new(session.SessionId, true, Guid.NewGuid()));

    Assert.False(result.IsSuccess);
    Assert.Equal(AssistSessionStatus.PendingConsent, (await GetSessionAsync(cache, session.SessionId)).Status);
    clients.Viewer.Verify(
      c => c.AssistConsentResponded(It.IsAny<AssistConsentRespondedDto>()),
      Times.Never);
  }

  [Fact]
  public async Task RespondToAssistConsent_DeniedWithStaleNonce_LeavesSessionPending()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user: null, connectionId: "assist-conn-1");

    var result = await hub.RespondToAssistConsent(new(session.SessionId, false, Guid.NewGuid()));

    Assert.True(result.IsSuccess);
    Assert.Equal(AssistSessionStatus.PendingConsent, (await GetSessionAsync(cache, session.SessionId)).Status);
    clients.Viewer.Verify(
      c => c.AssistConsentResponded(It.IsAny<AssistConsentRespondedDto>()),
      Times.Never);
  }

  [Fact]
  public async Task RespondToAssistConsent_Denied_ReturnsSessionToWaiting()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    var (hub, clients) = await CreateHubAsync(testApp, user: null, connectionId: "assist-conn-1");

    var result = await hub.RespondToAssistConsent(new(session.SessionId, false, await GetNonceAsync(cache, session.SessionId)));

    Assert.True(result.IsSuccess);
    Assert.Equal(AssistSessionStatus.WaitingForViewer, (await GetSessionAsync(cache, session.SessionId)).Status);
    clients.Viewer.Verify(
      c => c.AssistConsentResponded(It.Is<AssistConsentRespondedDto>(dto => !dto.Approved)),
      Times.Once);
  }

  [Fact]
  public async Task RespondToAssistConsent_FromWrongConnection_ReturnsUnauthorized()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var cache = GetCache(testApp);
    var session = await cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    var (hub, _) = await CreateHubAsync(testApp, user: null, connectionId: "someone-else");

    var result = await hub.RespondToAssistConsent(new(session.SessionId, true, await GetNonceAsync(cache, session.SessionId)));

    Assert.False(result.IsSuccess);
    Assert.Equal("Unauthorized.", result.Reason);
    Assert.Equal(AssistSessionStatus.PendingConsent, (await GetSessionAsync(cache, session.SessionId)).Status);
  }

  /// <summary>
  /// SignalR populates a hub's typed Clients proxy during activation. Unit tests construct the hub
  /// directly, so the backing field is set through reflection here.
  /// </summary>
  private static void AttachClients(AssistHub hub, IHubCallerClients<IAssistHubClient> clients)
  {
    var field = typeof(Hub<IAssistHubClient>)
      .GetField("_clients", BindingFlags.Instance | BindingFlags.NonPublic);

    Assert.NotNull(field);
    field!.SetValue(hub, clients);
  }

  private static ClaimsPrincipal CreateAnonymousPrincipal() =>
    new(new ClaimsIdentity(authenticationType: null));

  private static async Task<(AssistHub Hub, HubClients Clients)> CreateHubAsync(
    TestApp testApp,
    AppUser? user,
    Guid? tenantId = null,
    string? connectionId = null,
    PartitionedRateLimiter<HubCallerContext>? sessionCreateLimiter = null)
  {
    var scope = testApp.Services.CreateScope();
    var hub = new AssistHub(
      scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
      testApp.Services.GetRequiredService<IAssistSessionCache>(),
      sessionCreateLimiter
        ?? testApp.Services.GetRequiredService<PartitionedRateLimiter<HubCallerContext>>(),
      scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>(),
      scope.ServiceProvider.GetRequiredService<IResourceDescriptorFactory>(),
      testApp.Services.GetRequiredService<ILogger<AssistHub>>());

    var principal = user is null ? CreateAnonymousPrincipal() : CreatePrincipal(user, tenantId ?? Guid.NewGuid());
    hub.Context = new TestHubCallerContext(principal, connectionId);

    // Two proxies keyed by connection id, so a test can tell which side was notified. An
    // unmatched id resolves to null and the assertion fails loudly.
    var assistClientProxy = new Mock<IAssistHubClient>();
    var viewerClientProxy = new Mock<IAssistHubClient>();
    var clients = new Mock<IHubCallerClients<IAssistHubClient>>();
    clients.Setup(c => c.Client("assist-conn-1")).Returns(assistClientProxy.Object);
    clients.Setup(c => c.Client("viewer-conn-1")).Returns(viewerClientProxy.Object);
    clients.Setup(c => c.Client(It.Is<string>(id => id != "assist-conn-1" && id != "viewer-conn-1")))
      .Returns((Mock.Of<IAssistHubClient>()));
    AttachClients(hub, clients.Object);

    await Task.CompletedTask;
    return (hub, new HubClients(assistClientProxy, viewerClientProxy));
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

  private static IAssistSessionCache GetCache(TestApp testApp) =>
    testApp.Services.GetRequiredService<IAssistSessionCache>();

  private static async Task<Guid> GetNonceAsync(IAssistSessionCache cache, Guid sessionId)
  {
    var session = await GetSessionAsync(cache, sessionId);
    Assert.NotNull(session.ConsentNonce);
    return session.ConsentNonce.Value;
  }

  private static async Task<AssistSession> GetSessionAsync(IAssistSessionCache cache, Guid sessionId)
  {
    var session = await cache.TryGetAsync(sessionId, TestContext.Current.CancellationToken);
    Assert.NotNull(session);
    return session;
  }

  private static async Task GrantAssistConnectAsync(TestApp testApp, AppUser user, Guid tenantId)
  {
    using var scope = testApp.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
      PermissionPrincipalKind.User,
      user.Id,
      PermissionNames.AssistSessionsConnect,
      PermissionScopeKind.Tenant,
      tenantId,
      tenantId,
      createdBy: null));
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  }

  /// <summary>
  /// The two mock proxies the hub notifies, one per connection id, so an assertion can tell which
  /// side received a push.
  /// </summary>
  private sealed record HubClients(
    Mock<IAssistHubClient> Assist,
    Mock<IAssistHubClient> Viewer);

  private sealed class TestHubCallerContext : HubCallerContext
  {
    private readonly CancellationTokenSource _connectionAborted = new();

    public TestHubCallerContext(ClaimsPrincipal user, string? connectionId = null)
    {
      User = user;
      UserIdentifier = user.FindFirst(UserClaimTypes.UserId)?.Value;
      ConnectionId = connectionId ?? Guid.NewGuid().ToString();
    }

    public override CancellationToken ConnectionAborted => _connectionAborted.Token;
    public override string ConnectionId { get; }
    public override IFeatureCollection Features { get; } = new FeatureCollection();
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override ClaimsPrincipal User { get; }
    public override string? UserIdentifier { get; }

    public override void Abort()
    {
      _connectionAborted.Cancel();
    }
  }
}
