using ControlR.Web.Server.Data.Helpers;

namespace ControlR.Web.Server.Tests;

public class PostgresQueryHelperTests
{
  [Fact]
  public void EscapeCharacterIsASingleBackslash()
  {
    // PostgreSQL requires the LIKE/ILIKE escape character to be exactly one character, and
    // EscapeLikePattern below emits backslash escapes. An empty or multi-character constant makes
    // every escaped query fail at runtime with SQLSTATE 22019, taking down device search, device
    // column filters, and authorization change-log search.
    Assert.Equal(@"\", PostgresQueryHelper.LikeEscapeCharacter);
    Assert.Equal(1, PostgresQueryHelper.LikeEscapeCharacter.Length);
  }

  [Fact]
  public void EscapeLikePattern_EscapesBackslashBeforeOtherWildcards()
  {
    // If backslash were escaped last, "%" would first become "\%" and the backslash pass would
    // turn that into "\\%", leaving a literal backslash followed by an unescaped wildcard.
    var result = "%".EscapeLikePattern();

    Assert.Equal("\\%", result);
    Assert.DoesNotContain("\\\\%", result);
  }

  [Theory]
  [InlineData("", "")]
  [InlineData("device", "device")]
  [InlineData("Web Server 1", "Web Server 1")]
  // % and _ must become literal rather than wildcards.
  [InlineData("50%", "50\\%")]
  [InlineData("host_1", "host\\_1")]
  [InlineData("%", "\\%")]
  [InlineData("_", "\\_")]
  // Backslash must be escaped, and it must not double-escape the sequences added for % and _.
  [InlineData(@"C:\path", @"C:\\path")]
  [InlineData(@"50\%", @"50\\\%")]
  [InlineData(@"\_ignored", @"\\\_ignored")]
  // Combined input: every wildcard in one string.
  [InlineData("100%_done", @"100\%\_done")]
  [InlineData(@"a%\b_c", @"a\%\\b\_c")]
  // Characters that are not LIKE wildcards must pass through untouched.
  [InlineData("a[b]{c}d", "a[b]{c}d")]
  [InlineData("it's \"quoted\"", "it's \"quoted\"")]
  public void EscapeLikePattern_EscapesWildcardsAndPreservesOtherCharacters(string input, string expected)
  {
    var result = input.EscapeLikePattern();

    Assert.Equal(expected, result);
  }

  [Fact]
  public void EscapeLikePattern_OnAlreadyEscapedInputIsNotIdempotent()
  {
    // Documents that escaping is applied to raw user input exactly once. Re-escaping an escaped
    // pattern would turn the escape character itself into a literal backslash.
    var once = "50%".EscapeLikePattern();
    var twice = once.EscapeLikePattern();

    Assert.Equal(@"50\%", once);
    Assert.Equal(@"50\\\%", twice);
  }

  [Fact]
  public void EscapeLikePattern_ThrowsOnNull()
  {
    Assert.Throws<ArgumentNullException>(() => PostgresQueryHelper.EscapeLikePattern(null!));
  }
}