using ControlR.Shared.Helpers;
using ControlR.Shared.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace ControlR.Agent.Services.Windows;

public interface IRegistryAccessor
{
    Result<int> GetRdpPort();
}

internal class RegistryAccessor(ILogger<RegistryAccessor> _logger) : IRegistryAccessor
{
    [SupportedOSPlatform("windows")]
    public Result<int> GetRdpPort()
    {
        try
        {
            var rdpKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Terminal Server\\WinStations\\RDP-Tcp", false);
            Guard.IsNotNull(rdpKey);
            var port = (int)rdpKey.GetValue("PortNumber", 3389);
            return Result.Ok(port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting RDP port from registry.");
            return Result.Fail<int>("Failed to determine RDP port.");
        }
    }
}