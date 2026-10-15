using ControlR.DesktopClient.Services;
using ControlR.Libraries.Avalonia.Theming;
using ControlR.Libraries.TestingUtilities.FileSystem;

namespace ControlR.DesktopClient.Tests;

public class DesktopSettingsStoreTests
{
  private const string SettingsPath = "/app/config/settings.json";

  [Fact]
  public void Current_WhenFileMissing_UsesDefaults()
  {
    var store = new DesktopSettingsStore(SettingsPath, new FakeFileSystem());

    Assert.Equal(AvaloniaThemeMode.Dark, store.Current.Theme);
    Assert.Null(store.Current.Language);
  }

  [Fact]
  public void Language_SurvivesTheNextLaunch()
  {
    var fileSystem = new FakeFileSystem();

    new DesktopSettingsStore(SettingsPath, fileSystem).SetLanguage("cs-CZ");

    Assert.Equal("cs-CZ", new DesktopSettingsStore(SettingsPath, fileSystem).Current.Language);
  }

  [Fact]
  public void ThemeChange_KeepsTheSavedLanguage()
  {
    var fileSystem = new FakeFileSystem();
    var store = new DesktopSettingsStore(SettingsPath, fileSystem);

    store.SetLanguage("cs-CZ");
    store.SetTheme(AvaloniaThemeMode.Light);

    Assert.Equal("cs-CZ", new DesktopSettingsStore(SettingsPath, fileSystem).Current.Language);
  }

  [Fact]
  public void Theme_SurvivesTheNextLaunch()
  {
    var fileSystem = new FakeFileSystem();

    new DesktopSettingsStore(SettingsPath, fileSystem).SetTheme(AvaloniaThemeMode.Light);

    var reloaded = new DesktopSettingsStore(SettingsPath, fileSystem);

    Assert.Equal(AvaloniaThemeMode.Light, reloaded.Current.Theme);
    Assert.Null(reloaded.Current.Language);
  }
}
