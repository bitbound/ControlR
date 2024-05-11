namespace ControlR.Streamer.Sidecar.Options;

public class StartupOptions
{
    public string StreamerPipeName { get; set; } = "";
    public int ParentProcessId { get; set; }
}
