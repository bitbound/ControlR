using ControlR.Libraries.Shared.Services.StateManagement;

namespace ControlR.Libraries.Viewer.Common.State;

public interface IMetricsState
{
  TimeSpan CurrentLatency { get; set; }
  CaptureMetricsDto? CurrentMetrics { get; set; }
  int CurrentQuality { get; set; }
  double MbpsIn { get; set; }
  double MbpsOut { get; set; }
}

public class MetricsState(ILogger<ObservableState> logger) : ObservableState(logger), IMetricsState
{
  private TimeSpan _currentLatency;
  private CaptureMetricsDto? _currentMetrics;
  private int _currentQuality;
  private double _mbpsIn;
  private double _mbpsOut;

  public TimeSpan CurrentLatency
  {
    get => _currentLatency;
    set
    {
      _currentLatency = value;
      NotifyStateChanged();
    }
  }
  public CaptureMetricsDto? CurrentMetrics
  {
    get => _currentMetrics;
    set
    {
      _currentMetrics = value;
      NotifyStateChanged();
    }
  }
  public int CurrentQuality
  {
    get => _currentQuality;
    set
    {
      _currentQuality = value;
      NotifyStateChanged();
    }
  }
  public double MbpsIn
  {
    get => _mbpsIn;
    set
    {
      _mbpsIn = value;
      NotifyStateChanged();
    }
  }
  public double MbpsOut
  {
    get => _mbpsOut;
    set
    {
      _mbpsOut = value;
      NotifyStateChanged();
    }
  }
}