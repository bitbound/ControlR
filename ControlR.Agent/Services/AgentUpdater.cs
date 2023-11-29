using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Extensions;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

namespace ControlR.Agent.Services;

internal interface IAgentUpdater : IHostedService
{
    Task CheckForUpdate(CancellationToken cancellationToken = default);
}

internal class AgentUpdater(
    IVersionApi _versionApi,
    IDownloadsApi _downloadsApi,
    IFileSystem _fileSystem,
    IProcessManager _processInvoker,
    IEnvironmentHelper _environmentHelper,
    ISettingsProvider _settings,
    ILogger<AgentUpdater> logger) : BackgroundService, IAgentUpdater
{
    private readonly string _agentDownloadUri = $"{_settings.ServerUri}downloads/{AppConstants.AgentFileName}";
    private readonly SemaphoreSlim _checkForUpdatesLock = new(1, 1);
    private readonly ILogger<AgentUpdater> _logger = logger;

    public async Task CheckForUpdate(CancellationToken cancellationToken = default)
    {
        using var logScope = _logger.BeginMemberScope();

        if (!await _checkForUpdatesLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("Failed to acquire lock in agent updater.  Aborting check.");
            return;
        }

        try
        {
            _logger.LogInformation("Beginning version check.");

            var versionResult = await _versionApi.GetCurrentAgentVersion();
            if (!versionResult.IsSuccess)
            {
                return;
            }

            var exeVersion = typeof(AgentUpdater).Assembly.GetName().Version;

            _logger.LogInformation(
                "Comparing local version {LocalVersion} to latest version {ServerVersion}",
                exeVersion,
                versionResult.Value);

            if (versionResult.Value == exeVersion)
            {
                _logger.LogInformation("Version is current.");
                return;
            }

            _logger.LogInformation("Update found. Downloading update.");

            var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ControlR_Update"));
            var tempPath = Path.Combine(tempDir.FullName, AppConstants.AgentFileName);

            if (_fileSystem.FileExists(tempPath))
            {
                _fileSystem.DeleteFile(tempPath);
            }

            var result = await _downloadsApi.DownloadAgent(tempPath, _agentDownloadUri);
            if (!result.IsSuccess)
            {
                _logger.LogCritical("Download failed.  Aborting update.");
                return;
            }

            // TODO: Sign Linux binary.
            if (OperatingSystem.IsWindows())
            {
                var cert = X509Certificate.CreateFromSignedFile(tempPath);
                var thumbprint = cert.GetCertHashString().Trim();

                if (!string.Equals(thumbprint, AppConstants.AgentCertificateThumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogCritical(
                        "The certificate thumbprint of the downloaded agent binary is invalid.  Aborting update.  " +
                        "Expected Thumbprint: {expected}.  Actual Thumbprint: {actual}.",
                        AppConstants.AgentCertificateThumbprint,
                        thumbprint);
                    return;
                }
            }

            _logger.LogInformation("Launching installer.");

            switch (_environmentHelper.Platform)
            {
                case Shared.Enums.SystemPlatform.Windows:
                    {
                        _processInvoker.Start(tempPath, "install");
                    }
                    break;

                case Shared.Enums.SystemPlatform.Linux:
                    {
                        await _processInvoker
                          .Start("sudo", $"chmod +x {tempPath}")
                          .WaitForExitAsync(cancellationToken);

                        _processInvoker.Start("/bin/bash", $"-c \"{tempPath} install &\"", true);
                    }
                    break;

                case Shared.Enums.SystemPlatform.MacOS:
                    {
                        await _processInvoker
                         .Start("sudo", $"chmod +x {tempPath}")
                         .WaitForExitAsync(cancellationToken);

                        _processInvoker.Start("/bin/zsh", $"-c \"{tempPath} install &\"", true);
                    }
                    break;

                default:
                    throw new PlatformNotSupportedException();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for updates.");
        }
        finally
        {
            _checkForUpdatesLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_environmentHelper.IsDebug)
        {
            return;
        }

        await CheckForUpdate(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckForUpdate(stoppingToken);
        }
    }
}