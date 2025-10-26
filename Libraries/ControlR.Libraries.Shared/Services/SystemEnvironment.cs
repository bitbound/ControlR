using System.Runtime.InteropServices;
using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Services;

public interface ISystemEnvironment
{
  int CurrentThreadId { get; }
  bool Is64Bit { get; }
  bool IsDebug { get; }
  bool IsWindows { get; }
  SystemPlatform Platform { get; }
  int ProcessId { get; }
  RuntimeId Runtime { get; }
  string SelfExtractDir { get; }
  string StartupDirectory { get; }
  string StartupExePath { get; }
}

public class SystemEnvironment : ISystemEnvironment
{
  public static SystemEnvironment Instance { get; } = new();
  public int CurrentThreadId => Environment.CurrentManagedThreadId;

  public bool Is64Bit => Environment.Is64BitOperatingSystem;
  public bool IsDebug
  {
    get
    {
#if DEBUG
      return true;
#else
      return false;
#endif
    }
  }

  public bool IsWindows => OperatingSystem.IsWindows();

  public SystemPlatform Platform
  {
    get
    {
      if (OperatingSystem.IsWindows())
      {
        return SystemPlatform.Windows;
      }

      if (OperatingSystem.IsLinux())
      {
        return SystemPlatform.Linux;
      }

      if (OperatingSystem.IsMacOS())
      {
        return SystemPlatform.MacOs;
      }

      if (OperatingSystem.IsMacCatalyst())
      {
        return SystemPlatform.MacCatalyst;
      }

      if (OperatingSystem.IsAndroid())
      {
        return SystemPlatform.Android;
      }

      if (OperatingSystem.IsIOS())
      {
        return SystemPlatform.Ios;
      }

      if (OperatingSystem.IsBrowser())
      {
        return SystemPlatform.Browser;
      }

      return SystemPlatform.Unknown;
    }
  }

  public int ProcessId => Environment.ProcessId;

  public RuntimeId Runtime
  {
    get
    {
      return RuntimeInformation.RuntimeIdentifier switch
      {
        "win-x64" => RuntimeId.WinX64,
        "win-x86" => RuntimeId.WinX86,
        "linux-x64" => RuntimeId.LinuxX64,
        "osx-x64" => RuntimeId.MacOsX64,
        "osx-arm64" => RuntimeId.MacOsArm64,
        _ => throw new PlatformNotSupportedException()
      };
    }
  }

  public string SelfExtractDir => AppDomain.CurrentDomain.BaseDirectory;

  public string StartupDirectory =>
      Path.GetDirectoryName(StartupExePath) ??
    throw new DirectoryNotFoundException("Unable to determine startup directory.");

  public string StartupExePath { get; } = Environment.ProcessPath ?? Environment.GetCommandLineArgs().First();
}