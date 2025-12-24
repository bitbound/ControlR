namespace ControlR.DesktopClient.Linux.XdgPortal;

public class PipeWireStreamInfo
{
  public uint NodeId { get; set; }
  public Dictionary<string, object> Properties { get; set; } = new();
  public int StreamIndex { get; set; }
}
