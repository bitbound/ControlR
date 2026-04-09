using System.Collections.Concurrent;

namespace ControlR.Libraries.WebSocketRelay.Client;

public interface IStreamMetrics
{
  TimeSpan GetCurrentLatency();
  double GetMbpsIn();
  double GetMbpsOut();
  void RecordBytesIn(TransferRecord record);
  void RecordBytesOut(TransferRecord record);
  void SetCurrentLatency(TimeSpan latency);
}

public class StreamMetrics(TimeProvider timeProvider) : IStreamMetrics
{
  private const double MaxQueueAgeSeconds = 3;

  private readonly ConcurrentQueue<TransferRecord> _bytesIn = new();
  private readonly ConcurrentQueue<TransferRecord> _bytesOut = new();
  private readonly TimeProvider _timeProvider = timeProvider;

  private TimeSpan _currentLatency;

  public TimeSpan GetCurrentLatency() => _currentLatency;

  public double GetMbpsIn()
  {
    CleanupQueue(_bytesIn);
    var totalBytes = _bytesIn.Sum(x => x.Size);
    return ToMegabits(totalBytes);
  }

  public double GetMbpsOut()
  {
    CleanupQueue(_bytesOut);
    var totalBytes = _bytesOut.Sum(x => x.Size);
    return ToMegabits(totalBytes);
  }

  public void RecordBytesIn(TransferRecord record)
  {
    _bytesIn.Enqueue(record);
  }

  public void RecordBytesOut(TransferRecord record)
  {
    _bytesOut.Enqueue(record);
  }

  public void SetCurrentLatency(TimeSpan latency)
  {
    _currentLatency = latency;
  }

  private static double ToMegabits(int totalBytes)
  {
    var totalBits = totalBytes * 8;
    var megabits = totalBits / 1_000_000.0;
    return megabits / MaxQueueAgeSeconds;
  }

  private void CleanupQueue(ConcurrentQueue<TransferRecord> queue)
  {
    var cutoffTime = _timeProvider.GetTimestamp() - _timeProvider.TimestampFrequency * (long)MaxQueueAgeSeconds;

    while (queue.TryPeek(out var record))
    {
      if (record.Timestamp < cutoffTime)
      {
        queue.TryDequeue(out _);
      }
      else
      {
        break;
      }
    }
  }
}
