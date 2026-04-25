using System.Diagnostics;
using System.Runtime.InteropServices;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Security;

namespace ControlR.DesktopClient.Windows.Services;

internal sealed class WindowsUrlLauncher(
  IWin32Interop win32Interop,
  ILogger<WindowsUrlLauncher> logger) : IUrlLauncher
{
  private readonly ILogger<WindowsUrlLauncher> _logger = logger;
  private readonly IWin32Interop _win32Interop = win32Interop;

  public bool Open(string target)
  {
    if (string.IsNullOrWhiteSpace(target))
    {
      return false;
    }

    if (!IsRunningAsSystemProcess())
    {
      return OpenWithShellExecute(target);
    }

    return _win32Interop.OpenUrlInActiveSession(target);
  }

  private bool IsRunningAsSystemProcess()
  {
    var processHandle = PInvoke.GetCurrentProcess();
    using var safeProcessHandle = new SafeProcessHandle(processHandle, ownsHandle: false);

    if (!PInvoke.OpenProcessToken(safeProcessHandle, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
    {
      _logger.LogWarning(
        "OpenProcessToken failed while checking launcher identity. Error: {Error} {Message}",
        Marshal.GetLastPInvokeError(),
        Marshal.GetLastPInvokeErrorMessage());
      return false;
    }

    using (token)
    {
      PInvoke.GetTokenInformation(
        token,
        TOKEN_INFORMATION_CLASS.TokenUser,
        null,
        out var tokenInfoLength);

      if (tokenInfoLength == 0)
      {
        _logger.LogWarning(
          "GetTokenInformation(TokenUser) size query failed while checking launcher identity. Error: {Error} {Message}",
          Marshal.GetLastPInvokeError(),
          Marshal.GetLastPInvokeErrorMessage());
        return false;
      }

      var tokenInfoBuffer = new byte[tokenInfoLength];

      if (!PInvoke.GetTokenInformation(
            token,
            TOKEN_INFORMATION_CLASS.TokenUser,
            tokenInfoBuffer,
            out _))
      {
        _logger.LogWarning(
          "GetTokenInformation(TokenUser) failed while checking launcher identity. Error: {Error} {Message}",
          Marshal.GetLastPInvokeError(),
          Marshal.GetLastPInvokeErrorMessage());
        return false;
      }

      unsafe
      {
        fixed (byte* tokenInfoPointer = tokenInfoBuffer)
        {
          var tokenUser = (TOKEN_USER*)tokenInfoPointer;
          if (!PInvoke.ConvertSidToStringSid(tokenUser->User.Sid, out var sidString))
          {
            _logger.LogWarning(
              "ConvertSidToStringSid failed while checking launcher identity. Error: {Error} {Message}",
              Marshal.GetLastPInvokeError(),
              Marshal.GetLastPInvokeErrorMessage());
            return false;
          }

          return string.Equals(sidString.ToString(), "S-1-5-18", StringComparison.Ordinal);
        }
      }
    }
  }

  private bool OpenWithShellExecute(string target)
  {
    try
    {
      Process.Start(new ProcessStartInfo
      {
        FileName = target,
        UseShellExecute = true
      });

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to open target {Target}.", target);
      return false;
    }
  }
}