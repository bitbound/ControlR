using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services;
using ControlR.Agent.Common.Services;
using ControlR.ApiClient;
using ControlR.ApiClient.Interfaces.Agent;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.AgentApi;
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
  public async Task CheckForUpdate_OnMac_SpawnsTheInstallerItDownloaded()
  {
    var fixture = new AgentMaintenanceServiceFixture();
    fixture.FileSystem.AddFile(fixture.BundleHashPath, "OLD_HASH");
    fixture.ServeMacBundle("NEW_HASH");

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    fixture.ProcessManager.Verify(
      x => x.Start("sudo", It.Is<string>(args => args.Contains("chmod +x", StringComparison.Ordinal))),
      Times.Once);

    var startInfo = fixture.MacInstallerStartInfo;
    Assert.NotNull(startInfo);

    // The installer leaves the agent's process group on its own, so the agent launches the file it
    // downloaded. Nothing reads a job definition written by the install already on disk, which is what
    // let the old launchd path exec a path the download never wrote to.
    Assert.Equal(fixture.MacInstallerDownloadPath, startInfo.FileName);
    Assert.Equal(
      new[]
      {
        "install",
        "--server-uri",
        _serverUri.ToString(),
        "--tenant-id",
        _tenantId.ToString(),
        "--instance-id",
        "instance-1"
      },
      startInfo.ArgumentList);

    // Redirection would leave the installer blocked on its next write once the agent is gone and no one
    // is draining the pipe any more.
    Assert.False(startInfo.UseShellExecute);
    Assert.False(startInfo.RedirectStandardOutput);
    Assert.False(startInfo.RedirectStandardError);

    // This installer boots the agent out, so waiting on it is waiting on this process's own death. No
    // launchctl call remains now that no job is involved.
    fixture.ProcessManager.Verify(
      x => x.StartAndWaitForExit(It.IsAny<ProcessStartInfo>(), It.IsAny<TimeSpan>()),
      Times.Never);
  }

  [Fact]
  public async Task CheckForUpdate_OnMac_TransfersTheReplacedInstallToTheInstaller()
  {
    var fixture = new AgentMaintenanceServiceFixture();
    var installedSettingsPath = "/etc/controlr/instance-1/appsettings.json";

    fixture.FileSystem.AddFile(fixture.BundleHashPath, "OLD_HASH");
    fixture.FileSystem.AddFile(installedSettingsPath, "{\"PrivateKey\":\"key\"}");

    fixture.PathProvider
      .Setup(x => x.GetAgentAppSettingsPath())
      .Returns(installedSettingsPath);
    fixture.PathProvider
      .Setup(x => x.GetSettingsDirectoryFor("AcmeCorp", "Acme Remote", "instance-1"))
      .Returns("/etc/acme_remote/instance-1");

    fixture.ServeMacBundle("NEW_HASH", brandName: "Acme Remote", publisher: "AcmeCorp");

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    var startInfo = fixture.MacInstallerStartInfo;
    Assert.NotNull(startInfo);

    // The installer retires the install it is replacing, and only this run knows which install that was.
    // Carrying it in the spawn arguments is what keeps two installs from signing as one device.
    Assert.Contains("--previous-brand-name", startInfo.ArgumentList);
    Assert.Contains("ControlR", startInfo.ArgumentList);
    Assert.Contains("--previous-publisher", startInfo.ArgumentList);
    Assert.Contains("Bitbound", startInfo.ArgumentList);
    Assert.Equal(fixture.MacInstallerDownloadPath, startInfo.FileName);
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
        Version = Version.Parse("1.2.3"),
        BrandName = "ControlR",
        Publisher = "Bitbound"
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
        Version = Version.Parse("1.2.3"),
        BrandName = "ControlR",
        Publisher = "Bitbound"
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

  [Fact]
  public async Task CheckForUpdate_WhenMigrationCannotHandOffSettings_DoesNotLaunchInstaller()
  {
    var fixture = new AgentMaintenanceServiceFixture();

    fixture.FileSystem.AddFile(fixture.BundleHashPath, "OLD_HASH");

    // Configured to a settings file that is not actually there, so the hand-off cannot happen.
    fixture.PathProvider
      .Setup(x => x.GetAgentAppSettingsPath())
      .Returns(@"C:\ProgramData\ControlR\instance-1\appsettings.json");

    fixture.AgentUpdateApi
      .Setup(x => x.GetBundleMetadata(RuntimeId.WinX64, It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok(new BundleMetadataDto
      {
        BundleDownloadUrl = "/downloads/win-x64/OtherBrand.Agent.bundle.zip",
        BundleSha256 = "NEW_HASH",
        InstallerDownloadUrl = "/downloads/win-x64/OtherBrand.Agent.Installer.exe",
        InstallerSha256 = "ANY",
        Runtime = RuntimeId.WinX64,
        Version = Version.Parse("1.2.3"),
        BrandName = "OtherBrand",
        Publisher = "OtherPublisher"
      }));

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    // Migrating without the settings file would register a new device while the server keeps trusting
    // the old key, so the device could never authenticate again. Refuse instead.
    fixture.DownloadsApi.Verify(
      x => x.DownloadFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
    fixture.ProcessManager.Verify(
      x => x.Start(It.IsAny<string>(), It.IsAny<string>()),
      Times.Never);
  }

  [Fact]
  public async Task CheckForUpdate_WhenMigrationHasNoSigningKey_DoesNotLaunchInstaller()
  {
    var fixture = new AgentMaintenanceServiceFixture();

    fixture.FileSystem.AddFile(fixture.BundleHashPath, "OLD_HASH");
    fixture.FileSystem.AddFile(@"C:\ProgramData\ControlR\instance-1\appsettings.json", "{}");

    fixture.PathProvider
      .Setup(x => x.GetAgentAppSettingsPath())
      .Returns(@"C:\ProgramData\ControlR\instance-1\appsettings.json");
    fixture.PathProvider
      .Setup(x => x.GetSettingsDirectoryFor("OtherPublisher", "OtherBrand", "instance-1"))
      .Returns(@"C:\ProgramData\OtherBrand\instance-1");

    // An install with no signing key could never authenticate as this device under a new brand, and the
    // server keeps trusting the key it already stored, so the record would be unrecoverable.
    fixture.SettingsProvider
      .SetupGet(x => x.PrivateKey)
      .Returns((string?)null);

    fixture.AgentUpdateApi
      .Setup(x => x.GetBundleMetadata(RuntimeId.WinX64, It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok(new BundleMetadataDto
      {
        BundleDownloadUrl = "/downloads/win-x64/OtherBrand.Agent.bundle.zip",
        BundleSha256 = "NEW_HASH",
        InstallerDownloadUrl = "/downloads/win-x64/OtherBrand.Agent.Installer.exe",
        InstallerSha256 = "ANY",
        Runtime = RuntimeId.WinX64,
        Version = Version.Parse("1.2.3"),
        BrandName = "OtherBrand",
        Publisher = "OtherPublisher"
      }));

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    fixture.DownloadsApi.Verify(
      x => x.DownloadFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
    fixture.ProcessManager.Verify(
      x => x.Start(It.IsAny<string>(), It.IsAny<string>()),
      Times.Never);
  }

  [Fact]
  public async Task CheckForUpdate_WhenServerBrandNameDiffers_MigratesAndHandsOffSettings()
  {
    var fixture = new AgentMaintenanceServiceFixture();
    var installedSettingsPath = @"C:\ProgramData\ControlR\instance-1\appsettings.json";
    var handedOffSettingsPath = @"C:\ProgramData\OtherBrand\instance-1\appsettings.json";

    fixture.FileSystem.AddFile(fixture.BundleHashPath, "OLD_HASH");
    fixture.FileSystem.AddFile(installedSettingsPath, "{\"PrivateKey\":\"key\"}");

    fixture.PathProvider
      .Setup(x => x.GetAgentAppSettingsPath())
      .Returns(installedSettingsPath);
    fixture.PathProvider
      .Setup(x => x.GetSettingsDirectoryFor("OtherPublisher", "OtherBrand", "instance-1"))
      .Returns(@"C:\ProgramData\OtherBrand\instance-1");

    var installerBytes = new byte[] { 1, 2, 3, 4, 5 };
    var installerSha256 = Convert.ToHexString(SHA256.HashData(installerBytes));
    var launchedInstallerArguments = string.Empty;
    var launchedProcess = new Mock<IProcess>();
    launchedProcess
      .Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    fixture.AgentUpdateApi
      .Setup(x => x.GetBundleMetadata(RuntimeId.WinX64, It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok(new BundleMetadataDto
      {
        BundleDownloadUrl = "/downloads/win-x64/OtherBrand.Agent.bundle.zip",
        BundleSha256 = "NEW_HASH",
        InstallerDownloadUrl = "/downloads/win-x64/OtherBrand.Agent.Installer.exe",
        InstallerSha256 = installerSha256,
        Runtime = RuntimeId.WinX64,
        Version = Version.Parse("1.2.3"),
        BrandName = "OtherBrand",
        Publisher = "OtherPublisher"
      }));

    fixture.DownloadsApi
      .Setup(x => x.DownloadFile(
        "/downloads/win-x64/OtherBrand.Agent.Installer.exe",
        It.IsAny<string>(),
        It.IsAny<CancellationToken>()))
      .Returns<string, string, CancellationToken>((_, destinationPath, _) =>
      {
        fixture.FileSystem.AddFile(destinationPath, installerBytes);
        return Task.FromResult(Result.Ok());
      });

    fixture.ProcessManager
      .Setup(x => x.Start(It.IsAny<string>(), It.IsAny<string>()))
      .Returns<string, string>((_, arguments) =>
      {
        launchedInstallerArguments = arguments;
        return launchedProcess.Object;
      });

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    // The new brand's installer reads this file before it builds its host, so identity has to arrive
    // as a file rather than as arguments.
    Assert.True(fixture.FileSystem.FileExists(handedOffSettingsPath));

    // The install being retired is named so the new installer can remove it.
    Assert.Contains("--previous-brand-name", launchedInstallerArguments, StringComparison.Ordinal);
    Assert.Contains("\"ControlR\"", launchedInstallerArguments, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CheckForUpdate_WhenServerHasNoInstanceIdOpinion_UpdatesWithoutMigrating()
  {
    var fixture = new AgentMaintenanceServiceFixture();
    fixture.FileSystem.AddFile(fixture.BundleHashPath, "OLD_HASH");

    // A tenant whose instance ids are switched off reports none. That is not a request to drop the id
    // this install already has, so the install stays where it is.
    fixture.DeploymentApi
      .Setup(x => x.GetDeploymentOptions(It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok(new AgentDeploymentOptionsDto(null)));

    fixture.ServeWindowsBundle("NEW_HASH");

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    Assert.Contains("\"--instance-id\" \"instance-1\"", fixture.LaunchedInstallerArguments, StringComparison.Ordinal);
    Assert.DoesNotContain("--previous-brand-name", fixture.LaunchedInstallerArguments, StringComparison.Ordinal);
    Assert.DoesNotContain("--previous-instance-id", fixture.LaunchedInstallerArguments, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CheckForUpdate_WhenServerInstanceIdDiffers_MigratesTheInstall()
  {
    var fixture = new AgentMaintenanceServiceFixture();
    var installedSettingsPath = @"C:\ProgramData\ControlR\instance-1\appsettings.json";
    var handedOffSettingsPath = @"C:\ProgramData\ControlR\other\appsettings.json";

    fixture.FileSystem.AddFile(fixture.BundleHashPath, "OLD_HASH");
    fixture.FileSystem.AddFile(installedSettingsPath, "{\"PrivateKey\":\"key\"}");

    fixture.PathProvider
      .Setup(x => x.GetAgentAppSettingsPath())
      .Returns(installedSettingsPath);
    fixture.PathProvider
      .Setup(x => x.GetSettingsDirectoryFor("Bitbound", "ControlR", "other"))
      .Returns(@"C:\ProgramData\ControlR\other");

    // The tenant moved this deployment to a different instance id. The brand did not change, so only
    // the instance id can be what makes this a migration.
    fixture.DeploymentApi
      .Setup(x => x.GetDeploymentOptions(It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok(new AgentDeploymentOptionsDto("other")));

    fixture.ServeWindowsBundle("NEW_HASH");

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    // Identity has to arrive at the directory the new install reads its own settings from, which moves
    // with the instance id.
    Assert.True(fixture.FileSystem.FileExists(handedOffSettingsPath));

    // The install is created at the instance id the server named, and the one left behind is named so
    // the new installer removes that one rather than the directory it is itself going to.
    Assert.Contains("\"--instance-id\" \"other\"", fixture.LaunchedInstallerArguments, StringComparison.Ordinal);
    Assert.Contains("--previous-instance-id", fixture.LaunchedInstallerArguments, StringComparison.Ordinal);
    Assert.Contains("\"instance-1\"", fixture.LaunchedInstallerArguments, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CheckForUpdate_WhenServerInstanceIdMatches_UpdatesWithoutMigrating()
  {
    var fixture = new AgentMaintenanceServiceFixture();
    fixture.FileSystem.AddFile(fixture.BundleHashPath, "OLD_HASH");

    // The hash moved, so an update runs, but the instance id the server reports is the one this
    // install already uses, so nothing here is a migration.
    fixture.ServeWindowsBundle("NEW_HASH");

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    Assert.Contains("\"--instance-id\" \"instance-1\"", fixture.LaunchedInstallerArguments, StringComparison.Ordinal);
    Assert.DoesNotContain("--previous-brand-name", fixture.LaunchedInstallerArguments, StringComparison.Ordinal);
    Assert.DoesNotContain("--previous-instance-id", fixture.LaunchedInstallerArguments, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CheckForUpdate_WhenServerPublisherDiffers_MigratesAndNamesTheInstallBeingReplaced()
  {
    var fixture = new AgentMaintenanceServiceFixture();
    var installedSettingsPath = @"C:\ProgramData\Bitbound\ControlR\instance-1\appsettings.json";
    var handedOffSettingsPath = @"C:\ProgramData\OtherPublisher\ControlR\instance-1\appsettings.json";

    fixture.FileSystem.AddFile(fixture.BundleHashPath, "OLD_HASH");
    fixture.FileSystem.AddFile(installedSettingsPath, "{\"PrivateKey\":\"key\"}");

    fixture.PathProvider
      .Setup(x => x.GetAgentAppSettingsPath())
      .Returns(installedSettingsPath);
    fixture.PathProvider
      .Setup(x => x.GetSettingsDirectoryFor("OtherPublisher", "ControlR", "instance-1"))
      .Returns(@"C:\ProgramData\OtherPublisher\ControlR\instance-1");

    // The publisher names the directory the install lives in, so a same-brand bundle from a different
    // vendor is a different install. Comparing brand alone would extract the new bundle into this
    // vendor's directory while the new build looked for itself in the other one.
    fixture.ServeWindowsBundle("NEW_HASH", publisher: "OtherPublisher");

    var updater = fixture.CreateMaintenanceService();

    await updater.CheckForUpdate(force: true, cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(fixture.FileSystem.FileExists(handedOffSettingsPath));
    Assert.Contains("\"--previous-brand-name\" \"ControlR\"", fixture.LaunchedInstallerArguments, StringComparison.Ordinal);
    Assert.Contains("\"--previous-publisher\" \"Bitbound\"", fixture.LaunchedInstallerArguments, StringComparison.Ordinal);
  }

  [Fact]
  public async Task RepairDesktopClient_OnMac_SpawnsInstallerWithRepairArguments()
  {
    var fixture = new AgentMaintenanceServiceFixture();
    fixture.ServeMacBundle("ANY_HASH");

    var updater = fixture.CreateMaintenanceService();

    await updater.RepairDesktopClient("test request", TestContext.Current.CancellationToken);

    var startInfo = fixture.MacInstallerStartInfo;
    Assert.NotNull(startInfo);

    // The repair command has to reach the installer as its own command. An argument list frozen by the
    // install would have run a full install with install-time values instead of repairing.
    Assert.Equal(
      new[] { "repair-desktop", "--instance-id", "instance-1" },
      startInfo.ArgumentList);
    Assert.Equal(fixture.MacInstallerDownloadPath, startInfo.FileName);
  }

  private sealed class AgentMaintenanceServiceFixture
  {
    public AgentMaintenanceServiceFixture()
    {
      var mockAgentApi = new Mock<IControlrAgentApi>();
      mockAgentApi
        .SetupGet(x => x.Updates)
        .Returns(AgentUpdateApi.Object);
      mockAgentApi
        .SetupGet(x => x.Deployment)
        .Returns(DeploymentApi.Object);

      ControlrApi
        .SetupGet(x => x.Agent)
        .Returns(mockAgentApi.Object);

      // Matching this install's own instance id, so a test that does not care about the instance id
      // sees no reason to migrate on that axis.
      DeploymentApi
        .Setup(x => x.GetDeploymentOptions(It.IsAny<CancellationToken>()))
        .ReturnsAsync(ApiResult.Ok(new AgentDeploymentOptionsDto("instance-1")));

      HostApplicationLifetime
        .SetupGet(x => x.ApplicationStopping)
        .Returns(CancellationToken.None);

      SettingsProvider
        .SetupGet(x => x.DisableAutoUpdate)
        .Returns(false);
      SettingsProvider
        .SetupGet(x => x.PrivateKey)
        .Returns("private-key");
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

    public Mock<IAgentUpdateApi> AgentUpdateApi { get; } = new();
    public string BundleHashPath { get; } = @"C:\ControlR\.controlr-bundle.sha256";
    public Mock<IControlrApi> ControlrApi { get; } = new();
    public Mock<IAgentDeploymentApi> DeploymentApi { get; } = new();
    public Mock<IDownloadsApi> DownloadsApi { get; } = new();
    public FakeFileSystem FileSystem { get; } = new('\\');
    public Mock<IHostApplicationLifetime> HostApplicationLifetime { get; } = new();
    public string LaunchedInstallerArguments { get; private set; } = string.Empty;
    public string MacInstallerDownloadPath { get; private set; } = string.Empty;
    public ProcessStartInfo? MacInstallerStartInfo { get; private set; }
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

    /// <summary>
    /// Serves a macOS bundle whose hash differs from the installed one, so an update runs, and records
    /// the spawn the installer was started with. macOS detaches the installer by process group rather
    /// than through a launchd job, so the ProcessStartInfo is the whole observable result.
    /// </summary>
    public void ServeMacBundle(string bundleSha256, string brandName = "ControlR", string publisher = "Bitbound")
    {
      var installerBytes = new byte[] { 1, 2, 3, 4, 5 };
      var installerSha256 = Convert.ToHexString(SHA256.HashData(installerBytes));
      var launchedProcess = new Mock<IProcess>();
      launchedProcess
        .SetupGet(x => x.Id)
        .Returns(4242);

      SystemEnvironment
        .SetupGet(x => x.Platform)
        .Returns(SystemPlatform.MacOs);
      SystemEnvironment
        .SetupGet(x => x.Runtime)
        .Returns(RuntimeId.MacOsArm64);

      AgentUpdateApi
        .Setup(x => x.GetBundleMetadata(RuntimeId.MacOsArm64, It.IsAny<CancellationToken>()))
        .ReturnsAsync(ApiResult.Ok(new BundleMetadataDto
        {
          BundleDownloadUrl = "/downloads/osx-arm64/ControlR.Agent.bundle.zip",
          BundleSha256 = bundleSha256,
          InstallerDownloadUrl = "/downloads/osx-arm64/ControlR.Agent.Installer",
          InstallerSha256 = installerSha256,
          Runtime = RuntimeId.MacOsArm64,
          Version = Version.Parse("1.2.3"),
          BrandName = brandName,
          Publisher = publisher
        }));

      DownloadsApi
        .Setup(x => x.DownloadFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .Returns<string, string, CancellationToken>((_, destinationPath, _) =>
        {
          MacInstallerDownloadPath = destinationPath;
          FileSystem.AddFile(destinationPath, installerBytes);
          return Task.FromResult(Result.Ok());
        });

      ProcessManager
        .Setup(x => x.Start(It.IsAny<string>(), It.IsAny<string>()))
        .Returns(launchedProcess.Object);
      ProcessManager
        .Setup(x => x.Start(It.IsAny<ProcessStartInfo>()))
        .Returns<ProcessStartInfo>((startInfo) =>
        {
          MacInstallerStartInfo = startInfo;
          return launchedProcess.Object;
        });
    }

    /// <summary>
    /// Serves a Windows bundle whose hash differs from the installed one, so an update runs, and
    /// records the arguments the installer was launched with.
    /// </summary>
    public void ServeWindowsBundle(string bundleSha256, string brandName = "ControlR", string publisher = "Bitbound")
    {
      var installerBytes = new byte[] { 1, 2, 3, 4, 5 };
      var installerSha256 = Convert.ToHexString(SHA256.HashData(installerBytes));
      var launchedProcess = new Mock<IProcess>();
      launchedProcess
        .Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

      AgentUpdateApi
        .Setup(x => x.GetBundleMetadata(RuntimeId.WinX64, It.IsAny<CancellationToken>()))
        .ReturnsAsync(ApiResult.Ok(new BundleMetadataDto
        {
          BundleDownloadUrl = "/downloads/win-x64/ControlR.Agent.bundle.zip",
          BundleSha256 = bundleSha256,
          InstallerDownloadUrl = "/downloads/win-x64/ControlR.Agent.Installer.exe",
          InstallerSha256 = installerSha256,
          Runtime = RuntimeId.WinX64,
          Version = Version.Parse("1.2.3"),
          BrandName = brandName,
          Publisher = publisher
        }));

      DownloadsApi
        .Setup(x => x.DownloadFile(
          It.IsAny<string>(),
          It.IsAny<string>(),
          It.IsAny<CancellationToken>()))
        .Returns<string, string, CancellationToken>((_, destinationPath, _) =>
        {
          FileSystem.AddFile(destinationPath, installerBytes);
          return Task.FromResult(Result.Ok());
        });

      ProcessManager
        .Setup(x => x.Start(It.IsAny<string>(), It.IsAny<string>()))
        .Returns<string, string>((_, arguments) =>
        {
          LaunchedInstallerArguments = arguments;
          return launchedProcess.Object;
        });
    }

    private sealed class NoopDisposable : IDisposable
    {
      public void Dispose()
      {
      }
    }
  }
}
