using ControlR.Devices.Common.Services;
using ControlR.Viewer.Services.Interfaces;
using Microsoft.Extensions.Logging;
using IFileSystem = ControlR.Devices.Common.Services.IFileSystem;

namespace ControlR.Viewer.Services.Windows;

internal class RdpLauncherWindows(
    IProcessManager _processes,
    IFileSystem _fileSystem,
    ILogger<RdpLauncherWindows> _logger) : IRdpLauncher
{
    public async Task<Result> LaunchRdp(int localPort)
    {
        try
        {
            var filePath = Path.Combine(Path.GetTempPath(), "ControlR.rdp");
            var rdpContent =
                $"full address:s:127.0.0.1:{localPort}\r\n" +
                $"authentication level:i:0\r\n" +
                $"smart sizing:i:1";
            await _fileSystem.WriteAllTextAsync(filePath, rdpContent);
            var process = _processes.Start("mstsc.exe", $"\"{filePath}\"", true);
            if (process?.HasExited == false)
            {
                return Result.Ok();
            }
            return Result.Fail("RDP process failed to start.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while launching RDP app.");
            return Result.Fail("Failed to start RDP.");
        }
    }
}