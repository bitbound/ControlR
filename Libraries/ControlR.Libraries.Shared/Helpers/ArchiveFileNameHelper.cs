namespace ControlR.Libraries.Shared.Helpers;

public static class ArchiveFileNameHelper
{
  public static string? NormalizeArchiveFileName(string? archiveFileName)
  {
    if (string.IsNullOrWhiteSpace(archiveFileName))
    {
      return null;
    }

    var normalizedFileName = Path.GetFileName(archiveFileName.Trim());
    if (string.IsNullOrWhiteSpace(normalizedFileName))
    {
      return null;
    }

    if (normalizedFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
    {
      return null;
    }

    if (!normalizedFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
    {
      normalizedFileName += ".zip";
    }

    return normalizedFileName;
  }
}
