using ControlR.DesktopClient.Services;
using ControlR.Libraries.Avalonia.Theming;
using Moq;

namespace ControlR.DesktopClient.Tests;

/// <summary>
/// The theme has to come from the saved setting and be written back on change, which is the whole
/// point of the settings store.
/// </summary>
public class ThemeProviderTests
{
  [Fact]
  public void CurrentThemeMode_StartsFromTheSavedTheme()
  {
    var store = new Mock<IDesktopSettingsStore>();
    store.SetupGet(x => x.Current).Returns(new DesktopSettings { Theme = AvaloniaThemeMode.Light });

    var provider = new ThemeProvider(store.Object);

    Assert.Equal(AvaloniaThemeMode.Light, provider.CurrentThemeMode);
  }

  [Fact]
  public void SetThemeMode_PersistsTheChoice()
  {
    var store = new Mock<IDesktopSettingsStore>();
    store.SetupGet(x => x.Current).Returns(new DesktopSettings { Theme = AvaloniaThemeMode.Dark });

    var provider = new ThemeProvider(store.Object);
    provider.SetThemeMode(AvaloniaThemeMode.Light);

    store.Verify(x => x.SetTheme(AvaloniaThemeMode.Light), Times.Once);
    Assert.Equal(AvaloniaThemeMode.Light, provider.CurrentThemeMode);
  }

  [Fact]
  public void ToggleTheme_PersistsTheResult()
  {
    var store = new Mock<IDesktopSettingsStore>();
    store.SetupGet(x => x.Current).Returns(new DesktopSettings { Theme = AvaloniaThemeMode.Dark });

    var provider = new ThemeProvider(store.Object);
    provider.ToggleTheme();

    store.Verify(x => x.SetTheme(AvaloniaThemeMode.Light), Times.Once);
  }
}
