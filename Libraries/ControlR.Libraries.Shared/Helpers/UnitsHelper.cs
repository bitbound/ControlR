namespace ControlR.Libraries.Shared.Helpers;

public static class UnitsHelper
{
  private static readonly string[] _fileSizeSuffixes = ["B", "KB", "MB", "GB", "TB", "PB"];
  
  public static string ToHumanReadableFileSize(double bytes)
  {
    if (bytes <= 0)
    {
      return "0 B";
    }
    var i = (int)Math.Floor(Math.Log(bytes) / Math.Log(1024));
    return $"{bytes / Math.Pow(1024, i):F1} {_fileSizeSuffixes[i]}";
  }
}