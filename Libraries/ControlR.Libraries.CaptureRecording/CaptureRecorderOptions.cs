using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.Libraries.CaptureRecording;

public sealed class CaptureRecorderOptions
{
  public int KeyFrameFrameInterval { get; set; } = 120;
  public ImageFormat KeyFrameImageFormat { get; set; } = ImageFormat.Png;
  public TimeSpan KeyFrameInterval { get; set; } = TimeSpan.FromSeconds(5);
  public int KeyFrameQuality { get; set; } = 100;
}
