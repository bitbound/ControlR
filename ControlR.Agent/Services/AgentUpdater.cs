using ControlR.Agent.Options;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Extensions;
using ControlR.Shared.Primitives;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ControlR.Agent.Services;

internal interface IAgentUpdater : IHostedService
{
    Task CheckForUpdate(CancellationToken cancellationToken = default);
    ManualResetEventAsync UpdateCheckCompletedSignal { get; }
}

internal class AgentUpdater(
    IVersionApi _versionApi,
    IDownloadsApi _downloadsApi,
    IFileSystem _fileSystem,
    IProcessManager _processInvoker,
    IEnvironmentHelper _environmentHelper,
    ISettingsProvider _settings,
    IOptions<InstanceOptions> _instanceOptions,
    ILogger<AgentUpdater> logger) : BackgroundService, IAgentUpdater
{
    private readonly string _agentDownloadUri = $"{_settings.ServerUri}downloads/{RuntimeInformation.RuntimeIdentifier}/{AppConstants.AgentFileName}";
    private readonly SemaphoreSlim _checkForUpdatesLock = new(1, 1);
    private readonly ILogger<AgentUpdater> _logger = logger;

    public ManualResetEventAsync UpdateCheckCompletedSignal { get; } = new ManualResetEventAsync(false);

    public async Task CheckForUpdate(CancellationToken cancellationToken = default)
    {
        if (_environmentHelper.IsDebug)
        {
            return;
        }

        using var logScope = _logger.BeginMemberScope();

        if (!await _checkForUpdatesLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("Failed to acquire lock in agent updater.  Aborting check.");
            return;
        }

        try
        {
            UpdateCheckCompletedSignal.Reset();

            _logger.LogInformation("Beginning version check.");

            var hashResult = await _versionApi.GetCurrentAgentHash();
            if (!hashResult.IsSuccess)
            {
                return;
            }

            using var fs = new FileStream(_environmentHelper.StartupExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var exeHash = await MD5.HashDataAsync(fs, cancellationToken);

            _logger.LogInformation(
                "Comparing local file hash {LocalFileHash} to latest file hash {ServerFileHash}",
                Convert.ToBase64String(exeHash),
                Convert.ToBase64String(hashResult.Value));

            if (hashResult.Value.SequenceEqual(exeHash))
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

            var result = await _downloadsApi.DownloadFile(_agentDownloadUri, tempPath);
            if (!result.IsSuccess)
            {
                _logger.LogCritical("Download failed.  Aborting update.");
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                using var cert = X509Certificate.CreateFromSignedFile(tempPath);
                var thumbprint = cert.GetCertHashString().Trim();

                using var selfCert = X509Certificate.CreateFromSignedFile(_environmentHelper.StartupExePath);
                var expectedThumbprint = selfCert.GetCertHashString().Trim();
                
                if (!string.Equals(thumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogCritical(
                        "The certificate thumbprint of the downloaded agent binary is invalid.  Aborting update.  " +
                        "Expected Thumbprint: {expected}.  Actual Thumbprint: {actual}.",
                        expectedThumbprint,
                        thumbprint);
                    return;
                }
            }

            _logger.LogInformation("Launching installer.");

            var installCommand = "install";
            if (_instanceOptions.Value.InstanceId is string instanceId)
            {
                installCommand += $" -i {instanceId}";
            }

            switch (_environmentHelper.Platform)
            {
                case Shared.Enums.SystemPlatform.Windows:
                    {
                        await _processInvoker
                            .Start(tempPath, installCommand)
                            .WaitForExitAsync(cancellationToken);
                    }
                    break;

                case Shared.Enums.SystemPlatform.Linux:
                    {
                        await _processInvoker
                          .Start("sudo", $"chmod +x {tempPath}")
                          .WaitForExitAsync(cancellationToken);

                        await _processInvoker.StartAndWaitForExit(
                            "/bin/bash", 
                            $"-c \"{tempPath} {installCommand} &\"", 
                            true, 
                            cancellationToken);
                    }
                    break;

                case Shared.Enums.SystemPlatform.MacOS:
                    {
                        await _processInvoker
                         .Start("sudo", $"chmod +x {tempPath}")
                         .WaitForExitAsync(cancellationToken);

                        await _processInvoker.StartAndWaitForExit(
                            "/bin/zsh",
                            $"-c \"{tempPath} {installCommand} &\"",
                            true,
                            cancellationToken);
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
            UpdateCheckCompletedSignal.Set();
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