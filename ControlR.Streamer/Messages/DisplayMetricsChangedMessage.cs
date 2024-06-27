namespace ControlR.Streamer.Messages;
public record DisplayMetricsChangedMessage(double Mbps, double GpuFps, double CpuFps, double Ips, int ImageQuality);