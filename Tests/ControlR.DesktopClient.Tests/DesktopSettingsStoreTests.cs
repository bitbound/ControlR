using ControlR.DesktopClient.Services;
using ControlR.Libraries.Avalonia.Theming;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.DesktopClient.Tests;

public class DesktopSettingsStoreTests
{
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
    return new DesktopSettingsStore(
      SettingsPath,
      fileSystem,
      NullLogger<DesktopSettingsStore>.Instance);
  }
}
