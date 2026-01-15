using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;

namespace ControlR.DesktopClient.Windows.Services;

internal interface IAeroPeekProvider
{
  bool SetAeroPeekEnabled(bool isEnabled);
}

internal unsafe class AeroPeekProvider(ILogger<AeroPeekProvider> logger) : IAeroPeekProvider
{
  private const string AeroPeekValueName = "EnableAeroPeek";
  private const string DwmKeyPath = @"Software\Microsoft\Windows\DWM";

  private readonly ILogger<AeroPeekProvider> _logger = logger;

  public bool SetAeroPeekEnabled(bool isEnabled)
  {
    try
    {
      _logger.LogInformation("Setting Aero Peek enabled state to {IsEnabled}", isEnabled);

      var identity = WindowsIdentity.GetCurrent();
      var isSystem = identity.User?.IsWellKnown(WellKnownSidType.LocalSystemSid) == true;

      if (isSystem)
      {
        _logger.LogDebug("Running as SYSTEM. Using WTSQueryUserToken to target logged-in user's registry.");
        return SetAeroPeekEnabledAsSystem(isEnabled);
      }
      else
      {
        _logger.LogDebug("Running as regular user. Using Registry.CurrentUser.");
        return SetAeroPeekEnabledAsUser(isEnabled);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error setting Aero Peek state in registry.");
      return false;
    }
  }

  private bool SetAeroPeekEnabledAsSystem(bool isEnabled)
  {
    try
    {
      var sessionId = (uint)Process.GetCurrentProcess().SessionId;

      HANDLE userTokenHandle = default;

      if (!PInvoke.WTSQueryUserToken(sessionId, ref userTokenHandle))
      {
        _logger.LogError("Failed to get user token for session {SessionId}", sessionId);
        return false;
      }

      using var userToken = new SafeFileHandle(userTokenHandle, ownsHandle: true);

      // First call to get buffer size
      PInvoke.GetTokenInformation(
        userToken,
        TOKEN_INFORMATION_CLASS.TokenUser,
        null,
        out var tokenInfoLength);

      var tokenInfoBuffer = new byte[tokenInfoLength];

      if (!PInvoke.GetTokenInformation(
            userToken,
            TOKEN_INFORMATION_CLASS.TokenUser,
            tokenInfoBuffer,
            out _))
      {
        _logger.LogError("Failed to get token user information");
        return false;
      }

      fixed (byte* pTokenInfo = tokenInfoBuffer)
      {
        var tokenUser = (TOKEN_USER*)pTokenInfo;
        if (!PInvoke.ConvertSidToStringSid(tokenUser->User.Sid, out var sidString))
        {
          _logger.LogError("Failed to convert SID to string");
          return false;
        }

        var userKeyPath = $"{sidString}\\{DwmKeyPath}";
        _logger.LogDebug("Targeting user registry path: HKEY_USERS\\{UserKeyPath}", userKeyPath);

        if (isEnabled)
        {
          using var key = Registry.Users.OpenSubKey(userKeyPath, writable: true);
          // Explicitly set to 1 to enable peek
          key?.SetValue(AeroPeekValueName, 1, RegistryValueKind.DWord);
        }
        else
        {
          using var key = Registry.Users.CreateSubKey(userKeyPath, writable: true);
          key.SetValue(AeroPeekValueName, 0, RegistryValueKind.DWord);
        }
      }

      _logger.LogInformation("Successfully set Aero Peek enabled state to {IsEnabled} via SYSTEM", isEnabled);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error setting Aero Peek state for SYSTEM user.");
      return false;
    }
  }

  private bool SetAeroPeekEnabledAsUser(bool isEnabled)
  {
    if (isEnabled)
    {
      using var key = Registry.CurrentUser.OpenSubKey(DwmKeyPath, writable: true);
      key?.SetValue(AeroPeekValueName, 1, RegistryValueKind.DWord);
    }
    else
    {
      using var key = Registry.CurrentUser.CreateSubKey(DwmKeyPath, writable: true);
      key.SetValue(AeroPeekValueName, 0, RegistryValueKind.DWord);
    }

    _logger.LogInformation("Successfully set Aero Peek enabled state to {IsEnabled} via CurrentUser", isEnabled);
    return true;
  }
}
