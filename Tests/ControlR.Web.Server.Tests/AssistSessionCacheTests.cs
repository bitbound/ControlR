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
  public void Create_CalledTwiceForSameConnection_ReplacesPreviousSession()
  {
    var first = _cache.Create("assist-conn-1");
    var second = _cache.Create("assist-conn-1");

    Assert.NotEqual(first.SessionId, second.SessionId);
    Assert.False(_cache.TryGet(first.SessionId, out _));
    Assert.True(_cache.TryGet(second.SessionId, out _));
    Assert.True(_cache.TryGetByCode(second.Code, out _));
    Assert.True(_cache.TryGetByAssistConnectionId("assist-conn-1", out var byConnection));
    Assert.Equal(second.SessionId, byConnection?.SessionId);
  }

  [Fact]
  public void Create_NewSession_ReturnsDigitCodeAndWaitingStatus()
  {
    var session = _cache.Create("assist-conn-1");

    Assert.Equal(AssistSessionStatus.WaitingForViewer, session.Status);
    Assert.Equal(AssistSessionCache.CodeLength, session.Code.Length);
    Assert.True(session.Code.All(char.IsDigit));
    Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
  }

  [Fact]
  public void RemoveByAssistConnectionId_WithMultipleSessions_RemovesOnlyMatchingSessions()
  {
    var mine = _cache.Create("assist-conn-1");
    var other = _cache.Create("assist-conn-2");

    var removed = _cache.RemoveByAssistConnectionId("assist-conn-1");

    Assert.Single(removed);
    Assert.Equal(mine.SessionId, removed[0].SessionId);
    Assert.True(_cache.TryGet(other.SessionId, out _));
  }

  [Fact]
  public void RemoveByViewerConnectionId_AfterConsentRequested_RemovesSession()
  {
    var session = _cache.Create("assist-conn-1");
    _ = _cache.TryBeginConsent(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, out _);

    var removed = _cache.RemoveByViewerConnectionId("viewer-conn-1");

    Assert.Single(removed);
    Assert.Equal(session.SessionId, removed[0].SessionId);
    Assert.False(_cache.TryGet(session.SessionId, out _));
  }

  [Fact]
  public void Snapshot_ReturnsAllSessions()
  {
    _ = _cache.Create("assist-conn-1");
    _ = _cache.Create("assist-conn-2");

    var snapshot = _cache.Snapshot();

    Assert.Equal(2, snapshot.Count);
  }

  [Fact]
  public void TryActivate_TransitionsAdvanceTimestampFromTimeProvider()
  {
    var session = _cache.Create("assist-conn-1");
    _timeProvider.Advance(TimeSpan.FromMinutes(5));
    _ = _cache.TryBeginConsent(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, out var pending);

    _timeProvider.Advance(TimeSpan.FromMinutes(3));
    _ = _cache.TryActivate(session.SessionId, out var active);

    Assert.Equal(_timeProvider.GetUtcNow(), active?.StatusChangedAt);
    Assert.True(active?.StatusChangedAt > pending?.StatusChangedAt);
  }

  [Fact]
  public void TryActivate_WhenPendingConsent_TransitionsToActive()
  {
    var session = _cache.Create("assist-conn-1");
    _ = _cache.TryBeginConsent(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", null, out _);

    var activated = _cache.TryActivate(session.SessionId, out var active);

    Assert.True(activated);
    Assert.Equal(AssistSessionStatus.Active, active?.Status);
  }

  [Fact]
  public void TryActivate_WhenWaitingForViewer_ReturnsFalse()
  {
    var session = _cache.Create("assist-conn-1");

    var activated = _cache.TryActivate(session.SessionId, out _);

    Assert.False(activated);
  }

  [Fact]
  public void TryBeginConsent_WhenAlreadyPendingConsent_ReturnsFalse()
  {
    var session = _cache.Create("assist-conn-1");
    _ = _cache.TryBeginConsent(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "First", null, out _);

    var began = _cache.TryBeginConsent(session.SessionId, "viewer-conn-2", Guid.NewGuid(), "Second", null, out _);

    Assert.False(began);
  }

  [Fact]
  public void TryBeginConsent_WhenWaitingForViewer_TransitionsToPendingConsentAndStoresViewer()
  {
    var session = _cache.Create("assist-conn-1");
    var viewerUserId = Guid.NewGuid();

    var began = _cache.TryBeginConsent(session.SessionId, "viewer-conn-1", viewerUserId, "Jane Tech", "jane@example.com", out var pending);

    Assert.True(began);
    Assert.Equal(AssistSessionStatus.PendingConsent, pending?.Status);
    Assert.Equal("viewer-conn-1", pending?.ViewerConnectionId);
    Assert.Equal(viewerUserId.ToString(), pending?.ViewerUserId);
    Assert.Equal("Jane Tech", pending?.ViewerDisplayName);
    Assert.Equal("jane@example.com", pending?.ViewerEmail);
  }

  [Fact]
  public void TryGetByCode_UnknownCode_ReturnsFalse()
  {
    _ = _cache.Create("assist-conn-1");

    var found = _cache.TryGetByCode("000000000", out _);

    Assert.False(found);
  }

  [Fact]
  public void TryReturnToWaiting_WhenPendingConsent_ClearsViewerInfo()
  {
    var session = _cache.Create("assist-conn-1");
    _ = _cache.TryBeginConsent(session.SessionId, "viewer-conn-1", Guid.NewGuid(), "Jane", "jane@example.com", out _);

    var returned = _cache.TryReturnToWaiting(session.SessionId, out var waiting);

    Assert.True(returned);
    Assert.Equal(AssistSessionStatus.WaitingForViewer, waiting?.Status);
    Assert.Null(waiting?.ViewerConnectionId);
    Assert.Null(waiting?.ViewerUserId);
    Assert.Null(waiting?.ViewerDisplayName);
    Assert.Null(waiting?.ViewerEmail);
  }
}
