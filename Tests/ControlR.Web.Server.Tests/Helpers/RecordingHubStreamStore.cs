using System.Diagnostics.CodeAnalysis;
using ControlR.Web.Server.Hubs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ControlR.Web.Server.Tests.Helpers;

/// <summary>
/// Wraps the real store and records the lifetime of every call that creates a session.
/// </summary>
/// <remarks>
/// Only a creating call records. A call that finds an existing session passes an expiration the store
/// throws away, so recording that one would let a test's own seed value satisfy an assertion about the
/// production call site's lifetime.
/// </remarks>
internal sealed class RecordingHubStreamStore(ILogger<HubStreamStore> logger, IMemoryCache memoryCache) : IHubStreamStore
{
  private readonly List<CreatedSession> _createdSessions = [];
  private readonly HubStreamStore _inner = new(logger, memoryCache);

  /// <summary>
  /// The sessions a caller created, in creation order, with the lifetime each one applied.
  /// </summary>
  public IReadOnlyList<CreatedSession> CreatedSessions => _createdSessions;

  public HubStreamSignaler<T> GetOrCreate<T>(Guid streamId, TimeSpan expiration)
  {
    if (!_inner.TryGet<T>(streamId, out _))
    {
      _createdSessions.Add(new CreatedSession(streamId, typeof(T), expiration));
    }

    return _inner.GetOrCreate<T>(streamId, expiration);
  }

  public bool TryGet<T>(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler<T>? signaler)
  {
    return _inner.TryGet(streamId, out signaler);
  }

  public bool TryRemove<T>(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler<T>? signaler)
  {
    return _inner.TryRemove(streamId, out signaler);
  }

  internal sealed record CreatedSession(Guid StreamId, Type ElementType, TimeSpan Expiration);
}
