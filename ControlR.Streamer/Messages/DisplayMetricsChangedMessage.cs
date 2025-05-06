namespace ControlR.Streamer.Messages;
public record DisplayMetricsChangedMessage(double Mbps, double Fps, double Ips, bool IsUsingGpu, int ImageQuality);