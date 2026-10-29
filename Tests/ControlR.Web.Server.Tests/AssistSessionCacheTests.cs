using ControlR.Web.Server.Services.Assist;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.Web.Server.Tests;

public class AssistSessionCacheTests
{
  private readonly AssistSessionCache _cache;
  private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

  public AssistSessionCacheTests()
  {
    _cache = new AssistSessionCache(_timeProvider);
  }

  [Fact]
  public async Task Create_CalledTwiceForSameConnection_ReplacesPreviousSession()
  {
    var ct = TestContext.Current.CancellationToken;
    var first = await _cache.CreateAsync("assist-conn-1", ct);
    var second = await _cache.CreateAsync("assist-conn-1", ct);

    Assert.NotEqual(first.SessionId, second.SessionId);
    Assert.Null(await _cache.TryGetAsync(first.SessionId, ct));
    Assert.NotNull(await _cache.TryGetAsync(second.SessionId, ct));
    Assert.NotNull(await _cache.TryGetByCodeAsync(second.Code, ct));
    var byConnection = await _cache.TryGetByAssistConnectionIdAsync("assist-conn-1", ct);
    Assert.Equal(second.SessionId, byConnection?.SessionId);
  }

  [Fact]
  public async Task Create_NewSession_ReturnsDigitCodeAndWaitingStatus()
  {
    var session = await _cache.CreateAsync("assist-conn-1", TestContext.Current.CancellationToken);

    Assert.Equal(AssistSessionStatus.WaitingForViewer, session.Status);
    Assert.Equal(AssistSessionCache.CodeLength, session.Code.Length);
    Assert.True(session.Code.All(char.IsDigit));
    Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
  }

  [Fact]
  public async Task Create_WhenGeneratedCodeCollides_RegeneratesUntilUnique()
  {
    var attempts = 0;
    string NextCode() => ++attempts == 1 ? "111111111" : "222222222";
    var cache = new AssistSessionCache(_timeProvider, NextCode);
    var ct = TestContext.Current.CancellationToken;

    var first = await cache.CreateAsync("assist-conn-1", ct);
    var second = await cache.CreateAsync("assist-conn-2", ct);

    Assert.Equal("111111111", first.Code);
    Assert.Equal("222222222", second.Code);
  }

  [Fact]
  public async Task RemoveByAssistConnectionId_WithMultipleSessions_RemovesOnlyMatchingSessions()
  {
    var ct = TestContext.Current.CancellationToken;
    var mine = await _cache.CreateAsync("assist-conn-1", ct);
    var other = await _cache.CreateAsync("assist-conn-2", ct);

    var removed = await _cache.RemoveByAssistConnectionIdAsync("assist-conn-1", ct);

    Assert.Single(removed);
    Assert.Equal(mine.SessionId, removed[0].SessionId);
    Assert.NotNull(await _cache.TryGetAsync(other.SessionId, ct));
  }

  [Fact]
  public async Task Snapshot_ReturnsAllSessions()
  {
    var ct = TestContext.Current.CancellationToken;
    _ = await _cache.CreateAsync("assist-conn-1", ct);
    _ = await _cache.CreateAsync("assist-conn-2", ct);

    var snapshot = await _cache.SnapshotAsync(ct);

    Assert.Equal(2, snapshot.Count);
  }

  [Fact]
  public async Task TryActivate_TransitionsAdvanceTimestampFromTimeProvider()
  {
    var ct = TestContext.Current.CancellationToken;
    var session = await _cache.CreateAsync("assist-conn-1", ct);
    _timeProvider.Advance(TimeSpan.FromMinutes(5));
    var pending = await _cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, ct);

    _timeProvider.Advance(TimeSpan.FromMinutes(3));
    var active = await _cache.TryActivateAsync(session.SessionId, ct);

    Assert.Equal(_timeProvider.GetUtcNow(), active?.StatusChangedAt);
    Assert.True(active?.StatusChangedAt > pending?.StatusChangedAt);
  }

  [Fact]
  public async Task TryActivate_WhenPendingConsent_TransitionsToActive()
  {
    var ct = TestContext.Current.CancellationToken;
    var session = await _cache.CreateAsync("assist-conn-1", ct);
    _ = await _cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, ct);

    var active = await _cache.TryActivateAsync(session.SessionId, ct);

    Assert.NotNull(active);
    Assert.Equal(AssistSessionStatus.Active, active.Status);
  }

  [Fact]
  public async Task TryActivate_WhenWaitingForViewer_ReturnsNull()
  {
    var ct = TestContext.Current.CancellationToken;
    var session = await _cache.CreateAsync("assist-conn-1", ct);

    Assert.Null(await _cache.TryActivateAsync(session.SessionId, ct));
  }

  [Fact]
  public async Task TryBeginConsent_WhenAlreadyPendingConsent_ReturnsNull()
  {
    var ct = TestContext.Current.CancellationToken;
    var session = await _cache.CreateAsync("assist-conn-1", ct);
    _ = await _cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "First", null, ct);

    Assert.Null(await _cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-2", Guid.NewGuid(), "Second", null, ct));
  }

  [Fact]
  public async Task TryBeginConsent_WhenWaitingForViewer_TransitionsToPendingConsentAndStoresViewer()
  {
    var ct = TestContext.Current.CancellationToken;
    var session = await _cache.CreateAsync("assist-conn-1", ct);
    var viewerUserId = Guid.NewGuid();

    var pending = await _cache.TryBeginConsentAsync(
      session.SessionId,
      "viewer-conn-1",
      viewerUserId,
      "Jane Tech",
      "jane@example.com",
      ct);

    Assert.NotNull(pending);
    Assert.Equal(AssistSessionStatus.PendingConsent, pending.Status);
    Assert.Equal("viewer-conn-1", pending.ViewerConnectionId);
    Assert.Equal(viewerUserId.ToString(), pending.ViewerUserId);
    Assert.Equal("Jane Tech", pending.ViewerDisplayName);
    Assert.Equal("jane@example.com", pending.ViewerEmail);
  }

  [Fact]
  public async Task TryGetByCode_UnknownCode_ReturnsNull()
  {
    var ct = TestContext.Current.CancellationToken;
    _ = await _cache.CreateAsync("assist-conn-1", ct);

    Assert.Null(await _cache.TryGetByCodeAsync("000000000", ct));
  }

  [Fact]
  public async Task TryReturnToWaiting_WhenPendingConsent_ClearsViewerInfo()
  {
    var ct = TestContext.Current.CancellationToken;
    var session = await _cache.CreateAsync("assist-conn-1", ct);
    _ = await _cache.TryBeginConsentAsync(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", "jane@example.com", ct);

    var waiting = await _cache.TryReturnToWaitingAsync(session.SessionId, ct);

    Assert.NotNull(waiting);
    Assert.Equal(AssistSessionStatus.WaitingForViewer, waiting.Status);
    Assert.Null(waiting.ViewerConnectionId);
    Assert.Null(waiting.ViewerUserId);
    Assert.Null(waiting.ViewerDisplayName);
    Assert.Null(waiting.ViewerEmail);
  }
}
