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
  private readonly AssistSessionCache _cache;
  private readonly Mock<IAssistHubClient> _clientProxy = new();
  private readonly Mock<IHubClients<IAssistHubClient>> _hubClients = new();
  private readonly Mock<IHubContext<AssistHub, IAssistHubClient>> _hubContext = new();
  private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

  public AssistSessionCleanupServiceTests()
  {
    _cache = new AssistSessionCache(_timeProvider);
    _ = _hubClients.Setup(c => c.Client(It.IsAny<string>())).Returns(_clientProxy.Object);
    _ = _hubContext.Setup(c => c.Clients).Returns(_hubClients.Object);
  }

  [Fact]
  public async Task HandleElapsed_ActiveSessionExpired_NotifiesAssistEnded()
  {
    var session = _cache.Create("assist-conn-1");
    _ = _cache.TryBeginConsent(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, out _);
    _ = _cache.TryActivate(session.SessionId, out _);
    _timeProvider.Advance(TimeSpan.FromHours(13));

    await RunCleanup();

    Assert.False(_cache.TryGet(session.SessionId, out _));
    _clientProxy.Verify(
      c => c.AssistSessionEnded(It.Is<AssistSessionEndedDto>(dto => dto.SessionId == session.SessionId)),
      Times.Exactly(2));
  }

  [Fact]
  public async Task HandleElapsed_ConsentExpired_NotifiesViewerAndAssistAndReturnsSessionToWaiting()
  {
    var session = _cache.Create("assist-conn-1");
    _ = _cache.TryBeginConsent(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, out _);
    _timeProvider.Advance(TimeSpan.FromMinutes(2));

    await RunCleanup();

    _ = _cache.TryGet(session.SessionId, out var current);
    Assert.Equal(AssistSessionStatus.WaitingForViewer, current?.Status);
    _clientProxy.Verify(
      c => c.AssistConsentResponded(It.Is<AssistConsentRespondedDto>(dto =>
        dto.SessionId == session.SessionId && !dto.Approved)),
      Times.Once);
    _clientProxy.Verify(
      c => c.AssistConsentTimedOut(It.Is<AssistConsentTimedOutDto>(dto => dto.SessionId == session.SessionId)),
      Times.Once);
  }

  [Fact]
  public async Task HandleElapsed_ConsentNotYetExpired_KeepsPendingConsentAndSendsNothing()
  {
    var session = _cache.Create("assist-conn-1");
    _ = _cache.TryBeginConsent(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, out _);
    _timeProvider.Advance(TimeSpan.FromSeconds(30));

    await RunCleanup();

    _ = _cache.TryGet(session.SessionId, out var current);
    Assert.Equal(AssistSessionStatus.PendingConsent, current?.Status);
    _clientProxy.Verify(
      c => c.AssistConsentResponded(It.IsAny<AssistConsentRespondedDto>()),
      Times.Never);
    _clientProxy.Verify(
      c => c.AssistConsentTimedOut(It.IsAny<AssistConsentTimedOutDto>()),
      Times.Never);
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
