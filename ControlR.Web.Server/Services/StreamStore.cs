using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Server.Services;

public interface IStreamStore
{
  void AddOrUpdate(Guid streamId, StreamSignaler signaler, Func<Guid, StreamSignaler, StreamSignaler> updateFactory);

  StreamSignaler GetOrAdd(Guid streamId, Func<Guid, StreamSignaler> createFactory);

  bool TryGet(Guid streamId, [NotNullWhen(true)] out StreamSignaler? signaler);
  bool TryRemove(Guid streamId, [NotNullWhen(true)] out StreamSignaler? signaler);
  Task<Result<StreamSignaler>> WaitForStreamSession(Guid streamId, string viewerConnectionId, CancellationToken cancellationToken);
}

public class StreamStore : IStreamStore
{
  private static readonly ConcurrentDictionary<Guid, StreamSignaler> _streamingSessions = new();
  private readonly ILogger<StreamStore> _logger;

  public StreamStore(ILogger<StreamStore> logger)
  {
    _logger = logger;
  }

  public void AddOrUpdate(Guid streamId, StreamSignaler signaler, Func<Guid, StreamSignaler, StreamSignaler> updateFactory)
  {
    _streamingSessions.AddOrUpdate(streamId, signaler, updateFactory);
  }

  public StreamSignaler GetOrAdd(Guid streamId, Func<Guid, StreamSignaler> createFactory)
  {
    return _streamingSessions.GetOrAdd(streamId, createFactory);
  }

  public bool TryGet(Guid streamId, [NotNullWhen(true)] out StreamSignaler? signaler)
  {
    return _streamingSessions.TryGetValue(streamId, out signaler);
  }

  public bool TryRemove(Guid streamId, [NotNullWhen(true)] out StreamSignaler? signaler)
  {
    return _streamingSessions.TryRemove(streamId, out signaler);
  }

  public async Task<Result<StreamSignaler>> WaitForStreamSession(
    Guid streamId, 
    string viewerConnectionId, 
    CancellationToken cancellationToken)
  {
    try
    {
      var session = _streamingSessions.GetOrAdd(streamId, key => new StreamSignaler(streamId));
      session.RequesterConnectionId = viewerConnectionId;

      await session.ReadySignal.Wait(cancellationToken);
      return Result.Ok(session);
    }
    catch (OperationCanceledException)
    {
      return Result
        .Fail<StreamSignaler>("Timed out while waiting for stream")
        .Log(_logger);
    }
    catch (Exception ex) 
    {
      _logger.LogError(ex, "Error while waiting for stream.");
      return Result.Fail<StreamSignaler>("An error occurred while waiting for the stream.");
    }
  }
}