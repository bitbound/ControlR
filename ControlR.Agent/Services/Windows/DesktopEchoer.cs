using ControlR.Agent.IpcDtos;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Hosting;
using SimpleIpc;

namespace ControlR.Agent.Services.Windows;

internal interface IDesktopEchoer
{
  Task EchoInputDesktop(string pipeName);
}

internal class DesktopEchoer(
  IIpcConnectionFactory ipcFactory,
  IHostApplicationLifetime appLifetime,
  IWin32Interop win32Interop,
  ILogger<DesktopEchoer> logger) : IDesktopEchoer
{
  public async Task EchoInputDesktop(string pipeName)
  {
    try
    {
      using var completedSignal = new ManualResetEventAsync();

      using var client = await ipcFactory.CreateClient(".", pipeName);
      client.On<DesktopRequestDto, DesktopResponseDto>(_ =>
      {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
        {
          throw new PlatformNotSupportedException();
        }

        if (win32Interop.GetInputDesktop(out var desktop))
        {
          return new DesktopResponseDto(desktop.Trim());
        }

        throw new InvalidOperationException("Failed to get the current input desktop.");
      });

      client.On<ShutdownRequestDto>(_ =>
      {
        completedSignal.Set();
        appLifetime.StopApplication();
      });

      var connectResult = await client.Connect(5000);
      if (!connectResult)
      {
        throw new InvalidOperationException("Failed to connect to the named pipe server.");
      }

      client.BeginRead(appLifetime.ApplicationStopping);

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(appLifetime.ApplicationStopping, cts.Token);
      await completedSignal.Wait(linked.Token);
    }
    catch (OperationCanceledException)
    {
      logger.LogWarning("Timed out while waiting for agent connection.");
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while reporting input desktop to agent.");
    }
  }
}