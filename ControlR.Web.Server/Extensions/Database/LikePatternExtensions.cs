namespace ControlR.Web.Server.Extensions.Database;

public static class LikePatternExtensions
{
  /// <summary>
  /// The escape character <see cref="EscapeLikePattern"/> emits escapes for. Must be passed as the
  /// <c>escapeChar</c> argument of every <see cref="EF.Functions.ILike(string, string, string)"/>
  /// call whose pattern was produced by <see cref="EscapeLikePattern"/>.
  /// </summary>
  /// <remarks>
  /// This is not optional. The Npgsql EF Core provider translates the two-argument
  /// <c>EF.Functions.ILike(match, pattern)</c> overload to <c>match ILIKE pattern ESCAPE ''</c>, and
  /// in PostgreSQL an empty ESCAPE string means "no escape character". A pattern containing
  /// backslash escapes passed to that overload therefore matches a literal backslash and leaves
  /// <c>%</c> and <c>_</c> acting as wildcards, so the escaping is silently inert.
  /// </remarks>
  public const string LikeEscapeCharacter = "\\";

  /// <summary>
  /// Escapes PostgreSQL <c>LIKE</c>/<c>ILIKE</c> wildcards in caller-supplied text so that the text
  /// is matched literally instead of contributing query semantics. Backslash is escaped first.
  /// Escaping it afterward would double-escape the sequences this method introduces for
  /// <c>%</c> and <c>_</c>.
  /// </summary>
  /// <remarks>
  /// The result is only correct when <see cref="LikeEscapeCharacter"/> is supplied to
  /// <c>EF.Functions.ILike</c>. Only the PostgreSQL <c>ILIKE</c> path needs this. The in-memory
  /// provider fallback uses literal <see cref="string"/> comparisons, which need no escaping.
  /// </remarks>
  public static string EscapeLikePattern(this string value)
  {
    ArgumentNullException.ThrowIfNull(value);

    return value
      .Replace("\\", "\\\\")
      .Replace("%", "\\%")
      .Replace("_", "\\_");
  }
}
