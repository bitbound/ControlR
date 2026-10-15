using System.Text.Json;
using ControlR.Libraries.Branding;

namespace ControlR.DesktopClient.Common.Tests;

public class LocalizationTests
{
  [Fact]
  public void AvailableLanguages_NameThemselvesInTheirOwnLanguage()
  {
    var languages = Localization.GetAvailableLanguages();

    Assert.Contains("English", languages.Select(x => x.DisplayName));
    Assert.Contains("Čeština", languages.Select(x => x.DisplayName));
  }

  [Theory]
  [InlineData("cs-CZ")]
  [InlineData("en-US")]
  public void BrandNameToken_IsReplacedWithTheBrand(string culture)
  {
    Localization.SetCulture(culture);

    var value = Localization.ChatWindowDefaultTitle;

    Assert.DoesNotContain("{brand_name}", value);
    Assert.Contains(BrandingConstants.BrandName, value);
  }

  [Fact]
  public void CsCz_DefinesTheSameKeysAsEnUs()
  {
    var czech = ReadKeys("cs-CZ");
    var english = ReadKeys("en-US");

    Assert.Empty(english.Except(czech));
    Assert.Empty(czech.Except(english));
  }

  // A saved or system culture can name a language that has no strings file. Starting in English is
  // the only option left, so this must not throw.
  [Fact]
  public void SetCulture_WhenNoStringsFileExists_FallsBackToEnglish()
  {
    Localization.SetCulture("en-US");
    var englishTitle = Localization.ChatWindowDefaultTitle;

    Localization.SetCulture("de-DE");

    Assert.Equal(englishTitle, Localization.ChatWindowDefaultTitle);

    Localization.SetCulture("en-US");
  }

  private static HashSet<string> ReadKeys(string culture)
  {
    var assembly = typeof(Localization).Assembly;
    var resourcePath = assembly
      .GetManifestResourceNames()
      .Single(x => x.EndsWith($"{culture}.json", StringComparison.Ordinal)
                   && x.Contains(".Resources.Strings.", StringComparison.Ordinal));

    using var stream = assembly.GetManifestResourceStream(resourcePath)
                       ?? throw new InvalidOperationException($"Missing {resourcePath}.");
    using var reader = new StreamReader(stream);

    var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());

    return strings is null
      ? throw new InvalidOperationException($"Unable to read {resourcePath}.")
      : [.. strings.Keys];
  }
}
