namespace ControlR.Libraries.NativeInterop.Unix.Linux.XdgPortal;

public class RemoteDesktopStartResult
{
  public required List<PipeWireStreamInfo> Streams { get; init; }
  public string? RestoreToken { get; init; }
}
