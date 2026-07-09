using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services;
using ControlR.Agent.Common.Services;
using ControlR.ApiClient;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics;
using System.Security.Cryptography;

namespace ControlR.Agent.Common.Tests;

public class AgentMaintenanceServiceTests
{
  private static readonly Uri _serverUri = new("https://controlr.example/");
  private static readonly Guid _tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

  [Fact]
  public async Task CheckForUpdate_OnMac_BootstrapsOneShotLaunchDaemon()
  {
    var fixture = new AgentMaintenanceServiceFixture();
    fixture.FileSystem.AddFile(fixture.BundleHashPath, "OLD_HASH");
    fixture.SystemEnvironment
      .SetupGet(x => x.Platform)
      .Returns(SystemPlatform.MacOs);
    fixture.SystemEnvironment
      .SetupGet(x => x.Runtime)
      .Returns(RuntimeId.MacOsArm64);

    var installerBytes = new byte[] { 1, 2, 3, 4, 5 };
    var installerSha256 = Convert.ToHexString(SHA256.HashData(installerBytes));
    var downloadedInstallerPath = string.Empty;
    var process = new Mock<IProcess>();
    process
      .Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    fixture.AgentUpdateApi
      .Setup(x => x.GetBundleMetadata(RuntimeId.MacOsArm64, It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok(new BundleMetadataDto
      {
        BundleDownloadUrl = "/downloads/osx-arm64/ControlR.Agent.bundle.zip",
        BundleSha256 = "NEW_HASH",
        InstallerDownloadUrl = "/downloads/osx-arm64/ControlR.Agent.Installer",
        InstallerSha256 = installerSha256,
        Runtime = RuntimeId.MacOsArm64,
        Version = Version.Parse("1.2.3")
      }));

    fixture.DownloadsApi
      .Setup(x => x.DownloadFile("/downloads/osx-arm64/ControlR.Agent.Installer", It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns<string, string, CancellationToken>((_, destinationPath, _) =>
      {
        downloadedInstallerPath = destinationPath;
        fixture.FileSystem.AddFile(destinationPath, installerBytes);
        return Task.FromResult(Result.Ok());
      });

    fixture.ProcessManager
      .Setup(x => x.Start("sudo", It.IsAny<string>()))
      .Returns(process.Object);

    var launchctlStartInfos = new List<ProcessStartInfo>();
    fixture.ProcessManager
      .Setup(x => x.StartAndWaitForExit(It.IsAny<ProcessStartInfo>(), It.IsAny<TimeSpan>()))
      .Returns<ProcessStartInfo, TimeSpan>((startInfo, _) =>
      {
        launchctlStartInfos.Add(startInfo);
        return Task.FromResult(0);
      });

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    fixture.ProcessManager.Verify(
      x => x.Start("sudo", It.Is<string>(args => args.Contains("chmod +x", StringComparison.Ordinal))),
      Times.Once);
    Assert.Equal(3, launchctlStartInfos.Count);
    Assert.Equal("sudo", launchctlStartInfos[0].FileName);
    Assert.Equal("launchctl bootout system/app.controlr.agent.installer.instance-1", string.Join(" ", launchctlStartInfos[0].ArgumentList));
    Assert.Equal("sudo", launchctlStartInfos[1].FileName);
    var expectedInstallerPath = Path.Combine(Path.GetTempPath(), "ControlR_Update", "instance-1", "ControlR.Agent.Installer");
    Assert.Equal(expectedInstallerPath, downloadedInstallerPath);
    var expectedPlistPath = "/Library/LaunchDaemons/app.controlr.agent.installer.instance-1.plist";
    Assert.Equal(
      $"launchctl bootstrap system {expectedPlistPath}",
      string.Join(" ", launchctlStartInfos[1].ArgumentList));
    Assert.Equal("sudo", launchctlStartInfos[2].FileName);
    Assert.Equal(
      "launchctl kickstart -k system/app.controlr.agent.installer.instance-1",
      string.Join(" ", launchctlStartInfos[2].ArgumentList));
  }

  [Fact]
  public async Task CheckForUpdate_WhenInstalledBundleHashDiffers_DownloadsAndLaunchesInstaller()
  {
    var fixture = new AgentMaintenanceServiceFixture();
    fixture.FileSystem.AddFile(fixture.BundleHashPath, "OLD_HASH");

    var installerBytes = new byte[] { 1, 2, 3, 4, 5 };
    var installerSha256 = Convert.ToHexString(SHA256.HashData(installerBytes));
    var downloadedInstallerPath = string.Empty;
    var launchedInstallerPath = string.Empty;
    var launchedInstallerArguments = string.Empty;
    var launchedProcess = new Mock<IProcess>();
    launchedProcess
      .Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    fixture.AgentUpdateApi
      .Setup(x => x.GetBundleMetadata(RuntimeId.WinX64, It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok(new BundleMetadataDto
      {
        BundleDownloadUrl = "/downloads/win-x64/ControlR.Agent.bundle.zip",
        BundleSha256 = "NEW_HASH",
        InstallerDownloadUrl = "/downloads/win-x64/ControlR.Agent.Installer.exe",
        InstallerSha256 = installerSha256,
        Runtime = RuntimeId.WinX64,
        Version = Version.Parse("1.2.3")
      }));

    fixture.DownloadsApi
      .Setup(x => x.DownloadFile("/downloads/win-x64/ControlR.Agent.Installer.exe", It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns<string, string, CancellationToken>((_, destinationPath, _) =>
      {
        downloadedInstallerPath = destinationPath;
        fixture.FileSystem.AddFile(destinationPath, installerBytes);
        return Task.FromResult(Result.Ok());
      });

    fixture.ProcessManager
      .Setup(x => x.Start(It.IsAny<string>(), It.IsAny<string>()))
      .Returns<string, string>((fileName, arguments) =>
      {
        launchedInstallerPath = fileName;
        launchedInstallerArguments = arguments;
        return launchedProcess.Object;
      });

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    Assert.EndsWith("ControlR.Agent.Installer.exe", downloadedInstallerPath, StringComparison.OrdinalIgnoreCase);
    Assert.Equal(downloadedInstallerPath, launchedInstallerPath);
    Assert.Contains("install", launchedInstallerArguments, StringComparison.Ordinal);
    Assert.Contains("--server-uri", launchedInstallerArguments, StringComparison.Ordinal);
    Assert.Contains("\"https://controlr.example/\"", launchedInstallerArguments, StringComparison.Ordinal);
    Assert.Contains("--tenant-id", launchedInstallerArguments, StringComparison.Ordinal);
    Assert.Contains(_tenantId.ToString(), launchedInstallerArguments, StringComparison.Ordinal);
    Assert.Contains("--instance-id", launchedInstallerArguments, StringComparison.Ordinal);
    Assert.Contains("\"instance-1\"", launchedInstallerArguments, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CheckForUpdate_WhenInstalledBundleHashMatches_DoesNotDownloadOrLaunchInstaller()
  {
    var fixture = new AgentMaintenanceServiceFixture();
    fixture.FileSystem.AddFile(fixture.BundleHashPath, "ABC123");

    fixture.AgentUpdateApi
      .Setup(x => x.GetBundleMetadata(RuntimeId.WinX64, It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok(new BundleMetadataDto
      {
        BundleDownloadUrl = "/downloads/win-x64/ControlR.Agent.bundle.zip",
        BundleSha256 = "ABC123",
        InstallerDownloadUrl = "/downloads/win-x64/ControlR.Agent.Installer.exe",
        InstallerSha256 = "DEF456",
        Runtime = RuntimeId.WinX64,
        Version = Version.Parse("1.2.3")
      }));

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    fixture.DownloadsApi.Verify(
      x => x.DownloadFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
    fixture.ProcessManager.Verify(
      x => x.Start(It.IsAny<string>(), It.IsAny<string>()),
      Times.Never);
    fixture.AgentUpdateApi.Verify(
      x => x.GetBundleMetadata(RuntimeId.WinX64, It.IsAny<CancellationToken>()),
      Times.Once);
  }

  private sealed class AgentMaintenanceServiceFixture
  {
    public AgentMaintenanceServiceFixture()
    {
      var mockInternalApi = new Mock<IControlrInternalApi>();
      mockInternalApi
        .SetupGet(x => x.AgentUpdate)
        .Returns(AgentUpdateApi.Object);

      ControlrApi
        .SetupGet(x => x.Internal)
        .Returns(mockInternalApi.Object);

      HostApplicationLifetime
        .SetupGet(x => x.ApplicationStopping)
        .Returns(CancellationToken.None);

      SettingsProvider
        .SetupGet(x => x.DisableAutoUpdate)
        .Returns(false);
      SettingsProvider
        .SetupGet(x => x.ServerUri)
        .Returns(_serverUri);
      SettingsProvider
        .Setup(x => x.GetRequiredTenantId())
        .Returns(_tenantId);

      SystemEnvironment
        .SetupGet(x => x.Runtime)
        .Returns(RuntimeId.WinX64);
      SystemEnvironment
        .SetupGet(x => x.Platform)
        .Returns(SystemPlatform.Windows);

      PathProvider
        .Setup(x => x.GetBundleHashFilePath())
        .Returns(BundleHashPath);
    }

    public Mock<IInternalAgentUpdateApi> AgentUpdateApi { get; } = new();
    public string BundleHashPath { get; } = @"C:\ControlR\.controlr-bundle.sha256";
    public Mock<IControlrApi> ControlrApi { get; } = new();
    public Mock<IDownloadsApi> DownloadsApi { get; } = new();
    public FakeFileSystem FileSystem { get; } = new('\\');
    public Mock<IHostApplicationLifetime> HostApplicationLifetime { get; } = new();
    public Mock<IFileSystemPathProvider> PathProvider { get; } = new();
    public Mock<IProcessManager> ProcessManager { get; } = new();
    public Mock<IOptionsAccessor> SettingsProvider { get; } = new();
    public Mock<ISystemEnvironment> SystemEnvironment { get; } = new();

    public AgentMaintenanceService CreateMaintenanceService()
    {
      return new AgentMaintenanceService(
        TimeProvider.System,
        ControlrApi.Object,
        DownloadsApi.Object,
        FileSystem,
        PathProvider.Object,
        ProcessManager.Object,
        SystemEnvironment.Object,
        SettingsProvider.Object,
        HostApplicationLifetime.Object,
        Options.Create(new InstanceOptions { InstanceId = "instance-1" }),
        NullLogger<AgentMaintenanceService>.Instance);
    }

    private sealed class NoopDisposable : IDisposable
    {
      public void Dispose()
      {
      }
    }
  }
}
