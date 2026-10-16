using ControlR.DesktopClient.Common;

namespace ControlR.DesktopClient.Tests;

/// <summary>
/// A white-label build renames the assembly but not RootNamespace, so manifest names keep the
/// project spelling. Culture discovery has to ignore that head.
/// </summary>
public class LocalizationCultureTests
{
  [Fact]
  public void GetCultures_IgnoresResourcesOutsideTheStringsDirectory()
  {
    var cultures = Localization.GetCultures([
      "Acme.DesktopClient.Common.Resources.Strings.en-US.json",
      "Acme.DesktopClient.Common.Resources.Icons.en-US.json",
      "Acme.DesktopClient.Common.Notes.json"
    ]);

    Assert.Equal(["en-US"], cultures);
  }

  [Fact]
  public void GetCultures_OnThisBuild_FindsTheEmbeddedStrings()
  {
    var cultures = Localization.GetCultures(
      typeof(Localization).Assembly.GetManifestResourceNames());

    Assert.Contains("cs-CZ", cultures);
    Assert.Contains("en-US", cultures);
  }

  [Fact]
  public void GetCultures_WhenTheAssemblyIsRenamedButTheManifestsAreNot_FindsEveryCulture()
  {
    var cultures = Localization.GetCultures([
      "Acme.DesktopClient.Common.Resources.Strings.cs-CZ.json",
      "Acme.DesktopClient.Common.Resources.Strings.en-US.json",
      "Acme.DesktopClient.Common.Resources.Strings.de-DE.json"
    ]);

    Assert.Equal(["cs-CZ", "en-US", "de-DE"], cultures);
  }
}
