using ControlR.Web.Client.DataValidation;

namespace ControlR.Web.Server.Tests;

public class DisplayNameValidatorTests
{
  [Theory]
  [InlineData("Léon", true)]
  [InlineData("José", true)]
  [InlineData("Müller", true)]
  [InlineData("日本語", true)]
  [InlineData("العربية", true)]
  [InlineData("John Doe", true)]
  [InlineData("John_Doe", true)]
  [InlineData("John-Doe", true)]
  [InlineData("Alice123", true)]
  [InlineData("", true)]
  [InlineData("Bob@Home", false)]
  [InlineData("Bob#123", false)]
  [InlineData("$pecial", false)]
  [InlineData("test!", false)]
  [InlineData("😀", false)]
  [InlineData("🎉", false)]
  [InlineData("Le'on", false)]
  [InlineData("Robert'); DROP TABLE Users;--", false)]
  public void IsMatch_ReturnsExpectedResult(string input, bool expectedIsMatch)
  {
    var isMatch = !Validators.DisplayNameValidator().IsMatch(input);
    Assert.Equal(expectedIsMatch, isMatch);
  }
}
