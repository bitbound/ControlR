using ControlR.Devices.Common.Services;
using ControlR.Shared.Helpers;
using Microsoft.Extensions.Logging;
using System.Reflection;
using IFileSystemCore = ControlR.Devices.Common.Services.IFileSystem;
using IFileSystemMaui = Microsoft.Maui.Storage.IFileSystem;

namespace ControlR.Viewer.Services.Windows;

public interface ITightVncLauncherWindows
{
    Task<Result> LaunchTightVnc(int localPort, string? password = null);
}

internal class TightVncLauncherWindows(
    IFileSystemCore _fileSystem,
    IFileSystemMaui _mauiFileSystem,
    IProcessManager _processes,
    ISettings _settings,
    ILogger<TightVncLauncherWindows> _logger) : ITightVncLauncherWindows
{
    private readonly string _tvnResourcesDir = Path.Combine(_mauiFileSystem.AppDataDirectory, "TightVNC");
    private readonly string _tvnViewerPath = Path.Combine(_mauiFileSystem.AppDataDirectory, "TightVNC", "tvnviewer.exe");

    public async Task<Result> LaunchTightVnc(int localPort, string? password = null)
    {
        var resourcesResult = await EnsureTightVncResources();
        if (!resourcesResult.IsSuccess)
        {
            return resourcesResult;
        }

        var args = $"-host=127.0.0.1 -port={_settings.LocalProxyPort} -scale=auto";
        if (!string.IsNullOrEmpty(password))
        {
            args += $" -password={password}";
        }

        var tvnProcess = _processes.Start(_tvnViewerPath, args, true);
        if (tvnProcess?.HasExited == false)
        {
            return Result.Ok();
        }
        return Result.Fail("RDP process failed to start.");
    }

    private async Task<Result> EnsureTightVncResources()
    {
        try
        {
            _fileSystem.CreateDirectory(_tvnResourcesDir);

            var assembly = typeof(TightVncLauncherWindows).Assembly;
            var assemblyRoot = assembly.GetName().Name;
            var resourcesNamespace = $"{assemblyRoot}.VncResources.";
            var resourceNames = assembly
                .GetManifestResourceNames()
                .Where(x => x.Contains(resourcesNamespace));

            foreach (var resource in resourceNames)
            {
                var fileName = resource.Replace(resourcesNamespace, string.Empty);
                var targetPath = Path.Combine(_tvnResourcesDir, fileName);
                if (!_fileSystem.FileExists(targetPath))
                {
                    _logger.LogInformation("TightVNC resource is missing.  Extracting {TightVncFileName}.", fileName);
                    using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
                    Guard.IsNotNull(resourceStream);
                    using var fs = _fileSystem.CreateFileStream(targetPath, FileMode.Create);
                    await resourceStream.CopyToAsync(fs);
                }
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            var result = Result.Fail(ex, "Failed to extract TightVNC resources.");
            _logger.LogResult(result);
            return result;
        }
    }
}