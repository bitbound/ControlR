namespace ControlR.Libraries.CaptureRecording;

public interface ICaptureRecorderFactory
{
  CaptureRecorder Create(Stream stream);
}

public sealed class CaptureRecorderFactory(
  TimeProvider? timeProvider = null,
  CaptureRecorderOptions? options = null) : ICaptureRecorderFactory
{
  private readonly CaptureRecorderOptions _options = options ?? new();
  private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

  public CaptureRecorder Create(Stream stream)
  {
    return new CaptureRecorder(stream, _timeProvider, _options);
  }
}
