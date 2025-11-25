namespace ControlR.Libraries.NativeInterop.Unix.Linux.XdgPortal;

public class ScreenCastStartResult
{
  public required List<PipeWireStreamInfo> Streams { get; init; }
  public string? RestoreToken { get; init; }
}
