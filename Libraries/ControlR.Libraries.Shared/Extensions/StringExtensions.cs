using System.Text;

namespace ControlR.Libraries.Shared.Extensions;

public static class StringExtensions
{
  public static string SanitizeForFileSystem(this string self)
  {
    var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
    return string.Concat(self.Aggregate(
      new StringBuilder(),
      (sb, c) => invalidCharacters.Contains(c) 
        ? sb.Append('_')
        : sb.Append(c)));
  }
}