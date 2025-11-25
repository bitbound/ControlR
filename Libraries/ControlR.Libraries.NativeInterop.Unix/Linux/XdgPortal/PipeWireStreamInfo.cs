namespace ControlR.Libraries.NativeInterop.Unix.Linux.XdgPortal;

public class PipeWireStreamInfo
{
  public uint NodeId { get; set; }
  public Dictionary<string, object> Properties { get; set; } = new();
}
