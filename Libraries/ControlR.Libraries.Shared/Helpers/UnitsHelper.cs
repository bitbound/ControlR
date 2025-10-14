namespace ControlR.Libraries.Shared.Helpers;

public static class UnitsHelper
{
  private static readonly string[] _fileSizeSuffixes = ["B", "KB", "MB", "GB", "TB", "PB"];
  private static readonly string[] _networkSpeedSuffixes = ["bps", "Kbps", "Mbps", "Gbps", "Tbps", "Pbps"];

  public static string ToHumanReadableFileSize(double bytes)
  {
    if (bytes <= 0)
    {
      return "0 B";
    }
    var i = (int)Math.Floor(Math.Log(bytes) / Math.Log(1024));
    return $"{bytes / Math.Pow(1024, i):F1} {_fileSizeSuffixes[i]}";
  }
  
  public static string ToHumanReadableNetworkSpeed(double bytesPerSecond)
  {
    if (bytesPerSecond <= 0)
    {
        return "0 bps";
    }

    var bitsPerSecond = bytesPerSecond * 8;
    var i = (int)Math.Floor(Math.Log(bitsPerSecond) / Math.Log(1000));
    if (i >= _networkSpeedSuffixes.Length)
    {
        i = _networkSpeedSuffixes.Length - 1;
    }
    
    var value = bitsPerSecond / Math.Pow(1000, i);
    return $"{value:F1} {_networkSpeedSuffixes[i]}";
  }
}