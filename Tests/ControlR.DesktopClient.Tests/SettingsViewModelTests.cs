using System.Diagnostics;
using ControlR.DesktopClient.Services;
using ControlR.DesktopClient.ViewModels;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.DesktopClient.Tests;

public class SettingsViewModelTests
{
  [Fact]
  public void RestartClient_WhenSupervised_ExitsWithoutSpawning()
  {
    // The agent's desktop-client watcher relaunches the client on every platform, so a spawn here
    // would leave two clients on macOS and Linux.
    var processManager = new Mock<IProcessManager>();
    var lifetime = new Mock<IHostApplicationLifetime>();
    var viewModel = CreateViewModel(lifetime: lifetime, processManager: processManager);

    viewModel.RestartClientCommand.Execute(null);

    processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Never);
    lifetime.Verify(x => x.StopApplication(), Times.Once);
  }

  [Fact]
  public void RestartClient_WhenUnsupervised_SpawnsItsOwnReplacement()
  {
    // The watcher skips its loop in a Debug build, so nothing else would bring the client back.
    var processManager = new Mock<IProcessManager>();
    processManager
      .Setup(x => x.Start(It.IsAny<ProcessStartInfo>()))
      .Returns(new Mock<IProcess>().Object);

    var lifetime = new Mock<IHostApplicationLifetime>();
    var viewModel = CreateViewModel(
      isDebug: true,
      lifetime: lifetime,
      processManager: processManager);

    viewModel.RestartClientCommand.Execute(null);

    processManager.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Once);
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
    bool isDebug = false,
    Mock<IDesktopSettingsStore>? settingsStore = null,
    Mock<IHostApplicationLifetime>? lifetime = null,
    Mock<IProcessManager>? processManager = null)
  {
    settingsStore ??= new Mock<IDesktopSettingsStore>();
    settingsStore.SetupGet(x => x.Current).Returns(new DesktopSettings());

    var environment = new Mock<ISystemEnvironment>();
    environment.Setup(x => x.IsDebug).Returns(isDebug);

    return new SettingsViewModel(
      new Mock<IThemeProvider>().Object,
      settingsStore.Object,
      processManager?.Object ?? new Mock<IProcessManager>().Object,
      lifetime?.Object ?? new Mock<IHostApplicationLifetime>().Object,
      environment.Object,
      NullLogger<SettingsViewModel>.Instance);
  }
}
