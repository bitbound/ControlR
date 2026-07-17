using System.Diagnostics;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using ControlR.Libraries.NativeInterop.Windows;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Windows.Services;

internal class PlatformIpcMessageHandlerWindows(
  IWin32Interop win32Interop,
  ILogger<PlatformIpcMessageHandlerWindows> logger) : IPlatformIpcMessageHandler
{
  private readonly ILogger<PlatformIpcMessageHandlerWindows> _logger = logger;
  private readonly IWin32Interop _win32Interop = win32Interop;

  public Task<DesktopSessionInfoResponseIpcDto> GetDesktopSessionInfo()
  {
    var sessionId = Process.GetCurrentProcess().SessionId;
    var username = Environment.UserName;

    var desktopName = "Default";
    if (_win32Interop.GetInputDesktopName(out var inputDesktopName))
    {
      desktopName = inputDesktopName;
    }

    var consoleSessionId = _win32Interop.GetConsoleSessionId();
    var isConsole = (uint)sessionId == consoleSessionId;
    var sessionType = isConsole ? DesktopSessionType.Console : DesktopSessionType.Rdp;
    var sessionName = isConsole ? "Console" : "RDP";

    return Task.FromResult(new DesktopSessionInfoResponseIpcDto(
      AreRemoteControlPermissionsGranted: true,
      DesktopName: desktopName,
      Name: sessionName,
      SystemSessionId: sessionId,
      SessionType: sessionType,
      Username: username));
  }

  public Task InvokeCtrlAltDel(InvokeCtrlAltDelRequestDto dto)
  {
    _logger.LogInformation("Handling Ctrl+Alt+Del request. Requester: {RequesterId}", dto.InvokerUserName);
    _win32Interop.InvokeCtrlAltDel();
    return Task.CompletedTask;
  }
}
