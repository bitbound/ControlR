using ControlR.DesktopClient.Services;
using ControlR.Libraries.Avalonia.Theming;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.DesktopClient.Tests;

public class DesktopSettingsStoreTests
{
  private const string LegacySettingsPath = "/app/config-legacy/settings.json";
  private const string SettingsPath = "/app/config/settings.json";

  [Fact]
  public void Current_WhenFileIsTruncated_UsesDefaults()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.WriteAllText(SettingsPath, """
      { "Theme": 1, "Langu
      """);

    var store = CreateStore(fileSystem);

    Assert.Equal(AvaloniaThemeMode.Dark, store.Current.Theme);
    Assert.Null(store.Current.Language);
  }

  [Fact]
  public void Current_WhenFileMissing_UsesDefaults()
  {
    var store = CreateStore(new FakeFileSystem());

    Assert.Equal(AvaloniaThemeMode.Dark, store.Current.Theme);
    Assert.Null(store.Current.Language);
  }

  [Fact]
  public void Current_WhenOnlyTheLegacySettingsFileExists_MovesItIntoPlace()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.WriteAllText(LegacySettingsPath, """
      { "Language": "cs-CZ" }
      """);

    var store = CreateStore(fileSystem);

    // The settings file moved into a publisher directory along with the install, and this client is the
    // only process that can put the old copy at the new path with the owner it had before.
    Assert.Equal("cs-CZ", store.Current.Language);
    Assert.True(fileSystem.FileExists(SettingsPath));
    Assert.False(fileSystem.FileExists(LegacySettingsPath));
  }

  [Fact]
  public void Current_WhenTheSettingsFileAlreadyExists_LeavesTheLegacyFileAlone()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.WriteAllText(SettingsPath, """
      { "Language": "cs-CZ" }
      """);
    fileSystem.WriteAllText(LegacySettingsPath, """
      { "Language": "de-DE" }
      """);

    var store = CreateStore(fileSystem);

    Assert.Equal("cs-CZ", store.Current.Language);
    Assert.True(fileSystem.FileExists(LegacySettingsPath));
  }

  [Fact]
  public void Language_SurvivesTheNextLaunch()
  {
    var fileSystem = new FakeFileSystem();

    CreateStore(fileSystem).SetLanguage("cs-CZ");

    Assert.Equal("cs-CZ", CreateStore(fileSystem).Current.Language);
  }

  [Fact]
  public void Save_LeavesNoTemporaryFile()
  {
    var fileSystem = new FakeFileSystem();

    CreateStore(fileSystem).SetTheme(AvaloniaThemeMode.Light);

    Assert.False(fileSystem.FileExists($"{SettingsPath}.tmp"));
  }

  [Fact]
  public void ThemeChange_KeepsTheSavedLanguage()
  {
    var fileSystem = new FakeFileSystem();
    var store = CreateStore(fileSystem);

    store.SetLanguage("cs-CZ");
    store.SetTheme(AvaloniaThemeMode.Light);

    Assert.Equal("cs-CZ", CreateStore(fileSystem).Current.Language);
  }

  [Fact]
  public void Theme_SurvivesTheNextLaunch()
  {
    var fileSystem = new FakeFileSystem();

    CreateStore(fileSystem).SetTheme(AvaloniaThemeMode.Light);

    var reloaded = CreateStore(fileSystem);

    Assert.Equal(AvaloniaThemeMode.Light, reloaded.Current.Theme);
    Assert.Null(reloaded.Current.Language);
  }

  private static DesktopSettingsStore CreateStore(IFileSystem fileSystem)
  {
    var paths = new Mock<IDesktopPathProvider>();
    paths.Setup(x => x.GetSettingsPath()).Returns(SettingsPath);
    paths.Setup(x => x.GetLegacySettingsPath()).Returns(LegacySettingsPath);

    return new DesktopSettingsStore(
      paths.Object,
      fileSystem,
      NullLogger<DesktopSettingsStore>.Instance);
  }
}
