using System.Runtime.Versioning;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Win32;

namespace ControlR.Agent.Shared.Services.Windows;

/// <summary>
/// Reads and writes Windows registry settings used by the agent service.
/// </summary>
public interface IRegistryAccessor
{
  /// <summary>
  /// Gets the <c>PromptOnSecureDesktop</c> policy value.
  /// </summary>
  /// <returns><c>true</c> if prompting on the secure desktop is enabled; otherwise, <c>false</c>.</returns>
  bool GetPromptOnSecureDesktop();

  /// <summary>
  /// Gets the RDP port number from the registry.
  /// </summary>
  /// <returns>A <see cref="Result{T}"/> containing the port number on success, or a failure message if the value cannot be read.</returns>
  Result<int> GetRdpPort();

  /// <summary>
  /// Sets the <c>PromptOnSecureDesktop</c> policy value.
  /// </summary>
  /// <param name="enabled"><c>true</c> to enable prompting on the secure desktop; <c>false</c> to disable it.</param>
  void SetPromptOnSecureDesktop(bool enabled);

  /// <summary>
  /// Sets an environment variable for a Windows service via its registry <c>Environment</c> multi-string value.
  /// </summary>
  /// <param name="serviceName">The name of the Windows service.</param>
  /// <param name="variableName">The environment variable name.</param>
  /// <param name="value">The environment variable value.</param>
  void SetServiceEnvironmentVariable(string serviceName, string variableName, string value);

  /// <summary>
  /// Enables or disables <c>SoftwareSASGeneration</c> so the app can simulate the Ctrl+Alt+Del secure attention sequence.
  /// </summary>
  /// <param name="isEnabled"><c>true</c> to enable; <c>false</c> to revert to the default.</param>
  void SetSoftwareSasGeneration(bool isEnabled);
}

internal class RegistryAccessor(
  IElevationChecker elevation,
  ILogger<RegistryAccessor> logger) : IRegistryAccessor
{
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

  [SupportedOSPlatform("windows")]
  public void SetServiceEnvironmentVariable(string serviceName, string variableName, string value)
  {
    try
    {
      using var baseKey =
        Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}", true);

      if (baseKey is null)
      {
        logger.LogWarning("Service registry key not found: {ServiceName}", serviceName);
        return;
      }

      var existing = (string[]?)baseKey.GetValue("Environment");
      var entry = $"{variableName}={value}";
      var newValues = existing is null
        ? new[] { entry }
        : [.. existing.Where(v => !v.StartsWith($"{variableName}=", StringComparison.OrdinalIgnoreCase)), entry];

      baseKey.SetValue("Environment", newValues, RegistryValueKind.MultiString);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while setting service environment variable.");
    }
  }

  [SupportedOSPlatform("windows")]
  public void SetSoftwareSasGeneration(bool isEnabled)
  {
    try
    {
      // Set Secure Attention Sequence policy to allow app to simulate Ctrl + Alt + Del.
      using var subkey =
        Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);

      if (isEnabled)
      {
        subkey?.SetValue("SoftwareSASGeneration", "3", RegistryValueKind.DWord);
      }
      else
      {
        subkey?.DeleteValue("SoftwareSASGeneration", false);
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while enabling SoftwareSASGeneration.");
    }
  }
}
