using Microsoft.Extensions.Hosting;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;
public interface ICaptureMetrics : IHostedService, IDisposable
{
  double Fps { get; }
  bool IsUsingGpu { get; }
  double Mbps { get; }
  void MarkBytesSent(int length);
  void MarkFrameSent();
  void SetIsUsingGpu(bool isUsingGpu);
  Task WaitForBandwidth(CancellationToken cancellationToken);
}
