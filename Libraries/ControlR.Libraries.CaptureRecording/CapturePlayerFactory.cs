using Microsoft.Extensions.Options;

namespace ControlR.Libraries.CaptureRecording;

public interface ICapturePlayerFactory
{
  ICapturePlayer Create(Stream stream);
}

internal sealed class CapturePlayerFactory(
  TimeProvider timeProvider,
  IOptionsMonitor<CapturePlayerOptions> options) : ICapturePlayerFactory
{
  public ICapturePlayer Create(Stream stream)
  {
    return new CapturePlayer(stream, timeProvider, options.CurrentValue);
  }
}
