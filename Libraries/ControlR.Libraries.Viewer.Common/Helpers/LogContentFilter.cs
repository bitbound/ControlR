namespace ControlR.Libraries.Viewer.Common.Helpers;

public static class LogContentFilter
{
  private const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

  public static string Apply(string contents, string? filter)
  {
    if (string.IsNullOrEmpty(contents) || string.IsNullOrWhiteSpace(filter))
    {
      return contents;
    }

    var lines = contents.Replace("\r\n", "\n").Split('\n');
    var matching = lines.Where(line => line.Contains(filter, Comparison));
    return string.Join('\n', matching);
  }
}
