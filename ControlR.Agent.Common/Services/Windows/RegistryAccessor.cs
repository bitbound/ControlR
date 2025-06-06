﻿using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Win32;

namespace ControlR.Agent.Common.Services.Windows;

public interface IRegistryAccessor
{
  void EnableSoftwareSas();
  bool GetPromptOnSecureDesktop();
  Result<int> GetRdpPort();
  void SetPromptOnSecureDesktop(bool enabled);
}

internal class RegistryAccessor(
  IElevationChecker elevation,
  ILogger<RegistryAccessor> logger) : IRegistryAccessor
{
  [SupportedOSPlatform("windows")]
  public void EnableSoftwareSas()
  {
    try
    {
      // Set Secure Attention Sequence policy to allow app to simulate Ctrl + Alt + Del.
      using var subkey =
        Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
      subkey?.SetValue("SoftwareSASGeneration", "3", RegistryValueKind.DWord);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while enabling SoftwareSASGeneration.");
    }
  }

  [SupportedOSPlatform("windows")]
  public bool GetPromptOnSecureDesktop()
  {
    try
    {
      using var subkey =
        Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", false);
      if (subkey?.GetValue("PromptOnSecureDesktop", 1) is int promptValue)
      {
        return promptValue == 1;
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting PromptOnSecureDesktop.");
    }

    return true;
  }

  [SupportedOSPlatform("windows")]
  public Result<int> GetRdpPort()
  {
    try
    {
      using var rdpKey =
        Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Terminal Server\\WinStations\\RDP-Tcp",
          false);
      Guard.IsNotNull(rdpKey);
      var port = (int)rdpKey.GetValue("PortNumber", 3389);
      return Result.Ok(port);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting RDP port from registry.");
      return Result.Fail<int>("Failed to determine RDP port.");
    }
  }

  [SupportedOSPlatform("windows")]
  public void SetPromptOnSecureDesktop(bool enabled)
  {
    try
    {
      if (!elevation.IsElevated())
      {
        return;
      }

      using var subkey =
        Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
      subkey?.SetValue("PromptOnSecureDesktop", enabled ? 1 : 0, RegistryValueKind.DWord);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while setting PromptOnSecureDesktop.");
    }
  }
}