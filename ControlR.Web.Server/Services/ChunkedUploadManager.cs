using System.Collections.Concurrent;
using ControlR.Libraries.Shared.Services.Buffers;

namespace ControlR.Web.Server.Services;

public interface IChunkedUploadManager
{
  Task<Guid> InitiateUpload(
    Guid deviceId,
    string targetDirectory,
    string fileName,
    long totalSize,
    bool overwrite,
    string connectionId);

  Task<bool> WriteChunk(Guid uploadId, int chunkIndex, byte[] data);
  Task<(bool Success, string? Message)> CompleteUpload(Guid uploadId);
  Task<bool> CancelUpload(Guid uploadId);
  MemoryStream? GetUploadStream(Guid uploadId);
  ChunkedUploadSession? GetSession(Guid uploadId);
}

public class ChunkedUploadSession
{
  public Guid UploadId { get; init; }
  public Guid DeviceId { get; init; }
  public string TargetDirectory { get; init; } = string.Empty;
  public string FileName { get; init; } = string.Empty;
  public long TotalSize { get; init; }
  public bool Overwrite { get; init; }
  public string ConnectionId { get; init; } = string.Empty;
  public MemoryStream DataStream { get; init; } = new();
  public HashSet<int> ReceivedChunks { get; init; } = [];
  public int TotalChunks { get; set; }
  public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
  public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}

public class ChunkedUploadManager : IChunkedUploadManager, IDisposable
{
  private readonly ConcurrentDictionary<Guid, ChunkedUploadSession> _activeSessions = new();
  private readonly IMemoryProvider _memoryProvider;
  private readonly ILogger<ChunkedUploadManager> _logger;
  private readonly Timer _cleanupTimer;
  private const int MaxSessionAgeMinutes = 60;

  public ChunkedUploadManager(IMemoryProvider memoryProvider, ILogger<ChunkedUploadManager> logger)
  {
    _memoryProvider = memoryProvider;
    _logger = logger;
    _cleanupTimer = new Timer(CleanupCallbackStatic, this, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
  }

  private static void CleanupCallbackStatic(object? state)
  {
    if (state is ChunkedUploadManager manager)
    {
      manager.CleanupCallback();
    }
  }

  public Task<Guid> InitiateUpload(
    Guid deviceId,
    string targetDirectory,
    string fileName,
    long totalSize,
    bool overwrite,
    string connectionId)
  {
    var uploadId = Guid.NewGuid();
    var session = new ChunkedUploadSession
    {
      UploadId = uploadId,
      DeviceId = deviceId,
      TargetDirectory = targetDirectory,
      FileName = fileName,
      TotalSize = totalSize,
      Overwrite = overwrite,
      ConnectionId = connectionId,
      DataStream = _memoryProvider.GetRecyclableStream()
    };

    if (!_activeSessions.TryAdd(uploadId, session))
    {
      _logger.LogError("Failed to add upload session {UploadId}", uploadId);
      session.DataStream.Dispose();
      throw new InvalidOperationException("Failed to create upload session");
    }

    _logger.LogInformation(
      "Initiated chunked upload {UploadId} for file {FileName} ({TotalSize} bytes) to device {DeviceId}",
      uploadId, fileName, totalSize, deviceId);

    return Task.FromResult(uploadId);
  }

  public async Task<bool> WriteChunk(Guid uploadId, int chunkIndex, byte[] data)
  {
    if (!_activeSessions.TryGetValue(uploadId, out var session))
    {
      _logger.LogWarning("Upload session {UploadId} not found", uploadId);
      return false;
    }

    if (!session.ReceivedChunks.Add(chunkIndex))
    {
      _logger.LogWarning("Chunk {ChunkIndex} already received for upload {UploadId}", chunkIndex, uploadId);
      return true; // Already received, consider it success
    }

    try
    {
      await session.DataStream.WriteAsync(data);
      session.LastActivityAt = DateTime.UtcNow;

      _logger.LogDebug("Wrote chunk {ChunkIndex} ({DataLength} bytes) for upload {UploadId}",
        chunkIndex, data.Length, uploadId);

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error writing chunk {ChunkIndex} for upload {UploadId}", chunkIndex, uploadId);
      return false;
    }
  }

  public Task<(bool Success, string? Message)> CompleteUpload(Guid uploadId)
  {
    if (!_activeSessions.TryGetValue(uploadId, out var session))
    {
      _logger.LogWarning("Upload session {UploadId} not found for completion", uploadId);
      return Task.FromResult<(bool, string?)>((false, "Upload session not found"));
    }

    if (session.TotalChunks > 0 && session.ReceivedChunks.Count != session.TotalChunks)
    {
      var message = $"Expected {session.TotalChunks} chunks but received {session.ReceivedChunks.Count}";
      _logger.LogWarning("Upload {UploadId} incomplete: {Message}", uploadId, message);
      return Task.FromResult<(bool, string?)>((false, message));
    }

    if (session.DataStream.Length != session.TotalSize)
    {
      var message = $"Expected {session.TotalSize} bytes but received {session.DataStream.Length}";
      _logger.LogWarning("Upload {UploadId} size mismatch: {Message}", uploadId, message);
      return Task.FromResult<(bool, string?)>((false, message));
    }

    _logger.LogInformation("Completed chunked upload {UploadId} for file {FileName}",
      uploadId, session.FileName);

    return Task.FromResult<(bool, string?)>((true, null));
  }

  public Task<bool> CancelUpload(Guid uploadId)
  {
    if (_activeSessions.TryRemove(uploadId, out var session))
    {
      session.DataStream.Dispose();
      _logger.LogInformation("Cancelled upload {UploadId}", uploadId);
      return Task.FromResult(true);
    }

    return Task.FromResult(false);
  }

  public MemoryStream? GetUploadStream(Guid uploadId)
  {
    return _activeSessions.TryGetValue(uploadId, out var session) ? session.DataStream : null;
  }

  public ChunkedUploadSession? GetSession(Guid uploadId)
  {
    return _activeSessions.TryGetValue(uploadId, out var session) ? session : null;
  }

  private void CleanupCallback()
  {
    try
    {
      var cutoffTime = DateTime.UtcNow.AddMinutes(-MaxSessionAgeMinutes);
      var staleSessionIds = _activeSessions
        .Where(kvp => kvp.Value.LastActivityAt < cutoffTime)
        .Select(kvp => kvp.Key)
        .ToList();

      foreach (var sessionId in staleSessionIds)
      {
        if (_activeSessions.TryRemove(sessionId, out var session))
        {
          session.DataStream.Dispose();
          _logger.LogInformation("Cleaned up stale upload session {UploadId} (Last activity: {LastActivity})", 
            sessionId, session.LastActivityAt);
        }
      }

      if (staleSessionIds.Count > 0)
      {
        _logger.LogInformation("Cleanup completed: removed {Count} stale upload sessions", staleSessionIds.Count);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during upload session cleanup");
    }
  }

  public void Dispose()
  {
    _cleanupTimer.Dispose();

    foreach (var session in _activeSessions.Values)
    {
      session.DataStream.Dispose();
    }

    _activeSessions.Clear();
    GC.SuppressFinalize(this);
  }
}
