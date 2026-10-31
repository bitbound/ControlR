using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Libraries.Hosting;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Services.Assist;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ControlR.Web.Server.Tests;

public class AssistSessionCleanupServiceTests
{
  private readonly Mock<IAssistHubClient> _assistClientProxy = new();
  private readonly AssistSessionCache _cache;
  private readonly Mock<IHubClients<IAssistHubClient>> _hubClients = new();
  private readonly Mock<IHubContext<AssistHub, IAssistHubClient>> _hubContext = new();
  private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
  private readonly Mock<IAssistHubClient> _viewerClientProxy = new();

  public AssistSessionCleanupServiceTests()
  {
    _cache = new AssistSessionCache(_timeProvider);
    _ = _hubClients.Setup(c => c.Client("assist-conn-1")).Returns(_assistClientProxy.Object);
    _ = _hubClients.Setup(c => c.Client("viewer-conn-1")).Returns(_viewerClientProxy.Object);
    _ = _hubContext.Setup(c => c.Clients).Returns(_hubClients.Object);
  }

  [Fact]
  public async Task HandleElapsed_ActiveSessionExpired_NotifiesAssistEnded()
  {
    var session = await _cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    var pending = await _cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    _ = await _cache.TryActivateAsync(session.SessionId, pending!.ConsentNonce!.Value, TestContext.Current.CancellationToken);
    _timeProvider.Advance(TimeSpan.FromHours(13));

    await RunCleanup();

    Assert.Null(await _cache.TryGetAsync(session.SessionId, TestContext.Current.CancellationToken));
    _viewerClientProxy.Verify(
      c => c.AssistSessionEnded(It.Is<AssistSessionEndedDto>(dto =>
        dto.SessionId == session.SessionId && dto.Reason == AssistSessionEndedReason.SessionExpired)),
      Times.Once);
    _assistClientProxy.Verify(
      c => c.AssistSessionEnded(It.Is<AssistSessionEndedDto>(dto =>
        dto.SessionId == session.SessionId && dto.Reason == AssistSessionEndedReason.SessionExpired)),
      Times.Once);
  }

  [Fact]
  public async Task HandleElapsed_ConsentExpired_NotifiesViewerAndAssistAndReturnsSessionToWaiting()
  {
    var session = await _cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await _cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    _timeProvider.Advance(TimeSpan.FromMinutes(2));

    await RunCleanup();

    var current = await _cache.TryGetAsync(session.SessionId, TestContext.Current.CancellationToken);
    Assert.Equal(AssistSessionStatus.WaitingForViewer, current?.Status);
    _viewerClientProxy.Verify(
      c => c.AssistConsentResponded(It.Is<AssistConsentRespondedDto>(dto =>
        dto.SessionId == session.SessionId && !dto.Approved && dto.Reason == AssistConsentRespondedReason.TimedOut)),
      Times.Once);
    _assistClientProxy.Verify(
      c => c.AssistConsentTimedOut(It.Is<AssistConsentTimedOutDto>(dto => dto.SessionId == session.SessionId)),
      Times.Once);
  }

  [Fact]
  public async Task HandleElapsed_ConsentNotYetExpired_KeepsPendingConsentAndSendsNothing()
  {
    var session = await _cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _ = await _cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, TestContext.Current.CancellationToken);
    _timeProvider.Advance(TimeSpan.FromSeconds(30));

    await RunCleanup();

    var current = await _cache.TryGetAsync(session.SessionId, TestContext.Current.CancellationToken);
    Assert.Equal(AssistSessionStatus.PendingConsent, current?.Status);
    _viewerClientProxy.Verify(
      c => c.AssistConsentResponded(It.IsAny<AssistConsentRespondedDto>()),
      Times.Never);
    _viewerClientProxy.Verify(
      c => c.AssistConsentTimedOut(It.IsAny<AssistConsentTimedOutDto>()),
      Times.Never);
    _assistClientProxy.Verify(
      c => c.AssistConsentResponded(It.IsAny<AssistConsentRespondedDto>()),
      Times.Never);
    _assistClientProxy.Verify(
      c => c.AssistConsentTimedOut(It.IsAny<AssistConsentTimedOutDto>()),
      Times.Never);
  }

  [Fact]
  public async Task HandleElapsed_WaitingExpired_NotifiesAssistEnded()
  {
    var session = await _cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);
    _timeProvider.Advance(TimeSpan.FromMinutes(31));

    await RunCleanup();

    Assert.Null(await _cache.TryGetAsync(session.SessionId, TestContext.Current.CancellationToken));
    _assistClientProxy.Verify(
      c => c.AssistSessionEnded(It.Is<AssistSessionEndedDto>(dto =>
        dto.SessionId == session.SessionId && dto.Reason == AssistSessionEndedReason.AccessCodeExpired)),
      Times.Once);
  }

  [Fact]
  public async Task HandleElapsed_WaitingSessionTransitionedSinceSnapshot_IsNotRemovedOrNotified()
  {
    var stale = new AssistSession(
      Guid.NewGuid(),
      "123456789",
      "token",
      "assist-conn-1",
      AssistSessionStatus.WaitingForViewer,
      _timeProvider.GetUtcNow() - TimeSpan.FromMinutes(31),
      _timeProvider.GetUtcNow() - TimeSpan.FromMinutes(31));

    var cache = new Mock<IAssistSessionCache>();
    _ = cache.Setup(c => c.SnapshotAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync([stale]);
    _ = cache.Setup(c => c.TryRemoveAsync(
        stale.SessionId,
        AssistSessionStatus.WaitingForViewer,
        stale.StatusChangedAt,
        It.IsAny<CancellationToken>()))
      .ReturnsAsync((AssistSession?)null);

    var service = new TestCleanupService(cache.Object, _hubContext.Object, _timeProvider);
    await service.RunOnce(TestContext.Current.CancellationToken);

    cache.Verify(
      c => c.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
      Times.Never);
    _assistClientProxy.Verify(c => c.AssistSessionEnded(It.IsAny<AssistSessionEndedDto>()), Times.Never);
    _viewerClientProxy.Verify(c => c.AssistSessionEnded(It.IsAny<AssistSessionEndedDto>()), Times.Never);
  }

  private Task RunCleanup()
  {
    var service = new TestCleanupService(_cache, _hubContext.Object, _timeProvider);
    return service.RunOnce(TestContext.Current.CancellationToken);
  }

  /// <summary>
  /// Exposes the protected periodic tick so tests can drive one cleanup pass directly.
  /// </summary>
  private sealed class TestCleanupService(
    IAssistSessionCache sessionCache,
    IHubContext<AssistHub, IAssistHubClient> assistHub,
    TimeProvider timeProvider)
    : AssistSessionCleanupBackgroundService(sessionCache, assistHub, timeProvider, NullLogger<PeriodicBackgroundService>.Instance)
  {
    public Task RunOnce(CancellationToken cancellationToken) => HandleElapsed(cancellationToken);
  }
}
