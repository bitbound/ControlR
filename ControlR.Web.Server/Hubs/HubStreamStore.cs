using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Hubs;

public interface IHubStreamStore
{
  void AddOrUpdate(Guid streamId, HubStreamSignaler signaler, Func<Guid, HubStreamSignaler, HubStreamSignaler> updateFactory);

  HubStreamSignaler GetOrAdd(Guid streamId, Func<Guid, HubStreamSignaler> createFactory);
  HubStreamSignaler GetOrCreate(Guid streamId);

  bool TryGet(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler? signaler);
  bool TryRemove(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler? signaler);
  Task<Result<HubStreamSignaler>> WaitForStreamSession(Guid streamId, string viewerConnectionId, TimeSpan timeout);
}

public class HubStreamStore(ILogger<HubStreamStore> logger) : IHubStreamStore
{
  private readonly ConcurrentDictionary<Guid, HubStreamSignaler> _streamingSessions = new();
  private readonly ILogger<HubStreamStore> _logger = logger;

  public void AddOrUpdate(Guid streamId, HubStreamSignaler signaler, Func<Guid, HubStreamSignaler, HubStreamSignaler> updateFactory)
  {
    _streamingSessions.AddOrUpdate(streamId, signaler, updateFactory);
  }

  public HubStreamSignaler GetOrAdd(Guid streamId, Func<Guid, HubStreamSignaler> createFactory)
  {
    return _streamingSessions.GetOrAdd(streamId, createFactory);
  }

  public HubStreamSignaler GetOrCreate(Guid streamId)
  {
    return _streamingSessions.GetOrAdd(streamId, id => new HubStreamSignaler(id));
  }

  public bool TryGet(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler? signaler)
  {
    return _streamingSessions.TryGetValue(streamId, out signaler);
  }

  public bool TryRemove(Guid streamId, [NotNullWhen(true)] out HubStreamSignaler? signaler)
  {
    return _streamingSessions.TryRemove(streamId, out signaler);
  }

  public async Task<Result<HubStreamSignaler>> WaitForStreamSession(Guid streamId, string requesterConnectionId, TimeSpan timeout)
  {
    var session = _streamingSessions.GetOrAdd(streamId, key => new HubStreamSignaler(streamId));
    session.RequesterConnectionId = requesterConnectionId;

    try
    {
      await session.ReadySignal.Wait(timeout);
    }
    catch (OperationCanceledException)
    {
      _logger.LogError("Timed out while waiting for session.");
      return Result.Fail<HubStreamSignaler>("Timed out while waiting for session.");
    }

    return Result.Ok(session);
  }
}
