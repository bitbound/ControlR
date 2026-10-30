namespace ControlR.Assist.Services;

/// <summary>
/// Substitutes the first {0} token by concatenation. Values interpolated here can be
/// technician-controlled text, which must never reach <see cref="string.Format(string, object[])"/>
/// as raw arguments, where braces throw or act as format specifiers.
/// </summary>
internal static class SafeFormat
{
  private const string Token = "{0}";

  public static string Insert(string format, string value)
  {
    var index = format.IndexOf(Token, StringComparison.Ordinal);
    return index < 0 ?
      format + value :
      string.Concat(format.AsSpan(0, index), value, format.AsSpan(index + Token.Length));
  }
}
