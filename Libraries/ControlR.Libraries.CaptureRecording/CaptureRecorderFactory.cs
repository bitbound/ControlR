using Microsoft.Extensions.Options;

namespace ControlR.Libraries.CaptureRecording;

public interface ICaptureRecorderFactory
{
  ICaptureRecorder Create(Stream stream);
}

internal sealed class CaptureRecorderFactory(
  TimeProvider timeProvider,
  IOptionsMonitor<CaptureRecorderOptions> options) : ICaptureRecorderFactory
{
  public ICaptureRecorder Create(Stream stream)
  {
    return new CaptureRecorder(stream, timeProvider, options.CurrentValue);
  }
}
