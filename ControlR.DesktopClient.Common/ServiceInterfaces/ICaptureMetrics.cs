namespace ControlR.DesktopClient.Common.ServiceInterfaces;
public interface ICaptureMetrics : IDisposable
{
  double Fps { get; }
  double Ips { get; }
  bool IsQualityReduced { get; }
  bool IsUsingGpu { get; }
  double Mbps { get; }
  int Quality { get; }
  void MarkBytesSent(int length);
  void MarkFrameSent();
  void MarkIteration();
  void SetIsUsingGpu(bool isUsingGpu);
  void Start(CancellationToken cancellationToken);
  void Stop();
  Task WaitForBandwidth(CancellationToken cancellationToken);
}
