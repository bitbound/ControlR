namespace ControlR.Libraries.NativeInterop.Unix.Linux.XdgPortal;

public class ScreenCastStartResult
{
  public string? RestoreToken { get; init; }
  public required List<PipeWireStreamInfo> Streams { get; init; }
}
