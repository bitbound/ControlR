using ControlR.Agent.Dtos;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleIpc;

namespace ControlR.Agent.Services.Windows;

internal interface IDesktopEchoer
{
    Task EchoInputDesktop(string pipeName);
}

internal class DesktopEchoer(
    IIpcConnectionFactory _ipcFactory,
    IHostApplicationLifetime _appLifetime,
    IWin32Interop _win32Interop,
    ILogger<DesktopEchoer> _logger) : IDesktopEchoer
{
    public async Task EchoInputDesktop(string pipeName)
    {
        try
        {
            using var completedSignal = new ManualResetEventAsync();

            using var client = await _ipcFactory.CreateClient(".", pipeName);
            client.On<DesktopRequestDto, DesktopResponseDto>(_ =>
            {
                if (!OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
                {
                    throw new PlatformNotSupportedException();
                }

                if (_win32Interop.GetInputDesktop(out var desktop))
                {
                    return new DesktopResponseDto(desktop.Trim());
                }
                throw new InvalidOperationException("Failed to get the current input desktop.");
            });

            client.On<ShutdownRequestDto>(_ =>
            {
                completedSignal.Set();
                _appLifetime.StopApplication();
            });

            var connectResult = await client.Connect(5000);
            if (!connectResult)
            {
                throw new InvalidOperationException("Failed to connect to the named pipe server.");
            }
            client.BeginRead(_appLifetime.ApplicationStopping);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopping, cts.Token);
            await completedSignal.Wait(linked.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timed out while waiting for agent connection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reporting input desktop to agent.");
        }
    }
}
