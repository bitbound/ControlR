using ControlR.DesktopClient.Services;
using ControlR.DesktopClient.ViewModels;
using Microsoft.Extensions.Hosting;
using Moq;

namespace ControlR.DesktopClient.Tests;

public class SettingsViewModelTests
{
  [Fact]
  public void RestartClient_ExitsWithoutSpawning()
  {
    // The agent's desktop-client watcher relaunches the client on every platform, so a spawn here
    // would leave two clients on macOS and Linux.
    var lifetime = new Mock<IHostApplicationLifetime>();
    var viewModel = CreateViewModel(lifetime: lifetime);

    viewModel.RestartClientCommand.Execute(null);

    lifetime.Verify(x => x.StopApplication(), Times.Once);
  }

  [Fact]
  public void SelectedLanguage_PersistsTheChoice()
  {
    var settingsStore = new Mock<IDesktopSettingsStore>();
    settingsStore.SetupGet(x => x.Current).Returns(new DesktopSettings());

    var viewModel = CreateViewModel(settingsStore: settingsStore);
    var czech = viewModel.AvailableLanguages.Single(x => x.Culture == "cs-CZ");

    viewModel.SelectedLanguage = czech;

    settingsStore.Verify(x => x.SetLanguage("cs-CZ"), Times.Once);
  }

  private static SettingsViewModel CreateViewModel(
    Mock<IDesktopSettingsStore>? settingsStore = null,
    Mock<IHostApplicationLifetime>? lifetime = null)
  {
    settingsStore ??= new Mock<IDesktopSettingsStore>();
    settingsStore.SetupGet(x => x.Current).Returns(new DesktopSettings());

    return new SettingsViewModel(
      new Mock<IThemeProvider>().Object,
      settingsStore.Object,
      lifetime?.Object ?? new Mock<IHostApplicationLifetime>().Object);
  }
}
