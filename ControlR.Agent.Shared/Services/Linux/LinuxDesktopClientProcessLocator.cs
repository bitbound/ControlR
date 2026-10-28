using System.Runtime.Versioning;
using ControlR.Libraries.Shared.Services.FileSystem;

namespace ControlR.Agent.Shared.Services.Linux;

/// <summary>
/// A running DesktopClient process found by the executable it is running.
/// </summary>
/// <param name="ProcessId">The process id.</param>
/// <param name="OwnerUid">
/// The real uid that owns the process, or -1 when it could not be read. The agent spawns the login
/// screen client as root, while a user session client runs under its own uid, so this tells the two
/// apart without asking systemd.
/// </param>
/// <param name="InUserSlice">
/// Whether the process sits in a systemd user slice. The agent spawns the greeter client under its
/// own system service, so that client stays in the service's system slice, while a client started by
/// any user manager (root's included, since the desktop unit is enabled globally) sits in that
/// user's slice. An unreadable cgroup reports true, so the system-slice claim stays fail closed.
/// </param>
public readonly record struct LinuxDesktopClientProcess(int ProcessId, int OwnerUid, bool InUserSlice);

/// <summary>
/// Finds running DesktopClient processes by the executable they run, reading /proc directly.
/// </summary>
public interface ILinuxDesktopClientProcessLocator
{
  /// <summary>
  /// Returns every running process whose /proc/{pid}/exe resolves to <paramref name="executablePath"/>.
  /// </summary>
  IReadOnlyList<LinuxDesktopClientProcess> FindByExecutablePath(string executablePath);

  /// <summary>
  /// Re-checks that <paramref name="processId"/> is still running <paramref name="executablePath"/>
  /// and reports its owner and slice now. Returns null when it no longer runs that executable. Call
  /// this immediately before killing, so a pid recycled since it was found is not killed by mistake.
  /// </summary>
  LinuxDesktopClientProcess? GetRunningExecutable(int processId, string executablePath);
}

/// <remarks>
/// The kernel appends " (deleted)" to /proc/{pid}/exe once the binary has been replaced, which is
/// exactly the state of a client orphaned by an update, so the suffix is trimmed before comparing and
/// such a process still matches the path it was launched from. Process names are not used to narrow
/// the candidates because Linux truncates comm to 15 characters, which drops the tail of the
/// DesktopClient name and would hide the very process this is looking for.
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class LinuxDesktopClientProcessLocator(
  IFileSystem fileSystem,
  ILogger<LinuxDesktopClientProcessLocator> logger) : ILinuxDesktopClientProcessLocator
{
  private const string DeletedSuffix = " (deleted)";
  private const string ProcDirectory = "/proc";

  public IReadOnlyList<LinuxDesktopClientProcess> FindByExecutablePath(string executablePath)
  {
    var matches = new List<LinuxDesktopClientProcess>();

    if (string.IsNullOrWhiteSpace(executablePath))
    {
      return matches;
    }

    foreach (var processId in EnumerateProcessIds())
    {
      var resolvedPath = ResolveExecutablePath(processId);
      if (resolvedPath is null || !IsSameExecutable(resolvedPath, executablePath))
      {
        continue;
      }

      matches.Add(new LinuxDesktopClientProcess(processId, ReadOwnerUid(processId), ReadInUserSlice(processId)));
    }

    return matches;
  }

  public LinuxDesktopClientProcess? GetRunningExecutable(int processId, string executablePath)
  {
    if (string.IsNullOrWhiteSpace(executablePath))
    {
      return null;
    }

    var resolvedPath = ResolveExecutablePath(processId);
    if (resolvedPath is null || !IsSameExecutable(resolvedPath, executablePath))
    {
      return null;
    }

    return new LinuxDesktopClientProcess(processId, ReadOwnerUid(processId), ReadInUserSlice(processId));
  }

  private static bool IsSameExecutable(string resolvedPath, string executablePath)
  {
    var normalized = resolvedPath.EndsWith(DeletedSuffix, StringComparison.Ordinal)
      ? resolvedPath[..^DeletedSuffix.Length]
      : resolvedPath;

    return string.Equals(normalized, executablePath, StringComparison.Ordinal);
  }

  private IEnumerable<int> EnumerateProcessIds()
  {
    string[] directories;

    try
    {
      directories = fileSystem.GetDirectories(ProcDirectory);
    }
    catch (Exception ex)
    {
      logger.LogWarning(ex, "Failed to enumerate {ProcDirectory}.", ProcDirectory);
      return [];
    }

    var processIds = new List<int>(directories.Length);
    foreach (var directory in directories)
    {
      if (int.TryParse(Path.GetFileName(directory), out var processId))
      {
        processIds.Add(processId);
      }
    }

    return processIds;
  }

  private bool ReadInUserSlice(int processId)
  {
    try
    {
      // cgroup v2 gives one "0::/path" line; v1 gives one line per controller. Either way the
      // systemd path for a user session lives under user.slice.
      return fileSystem
        .ReadAllText($"{ProcDirectory}/{processId}/cgroup")
        .Contains("user.slice", StringComparison.Ordinal);
    }
    catch (Exception ex)
    {
      // Treat an unreadable cgroup as a user slice. Claiming the system slice is what authorizes a
      // kill, so the claim needs proof.
      logger.LogDebug(ex, "Failed to read the cgroup for PID {ProcessId}.", processId);
      return true;
    }
  }

  private int ReadOwnerUid(int processId)
  {
    try
    {
      var status = fileSystem.ReadAllText($"{ProcDirectory}/{processId}/status");
      foreach (var line in status.Split('\n'))
      {
        if (!line.StartsWith("Uid:", StringComparison.Ordinal))
        {
          continue;
        }

        var fields = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length >= 2 && int.TryParse(fields[1], out var uid))
        {
          return uid;
        }
      }
    }
    catch (Exception ex)
    {
      logger.LogDebug(ex, "Failed to read the owner uid for PID {ProcessId}.", processId);
    }

    return -1;
  }

  private string? ResolveExecutablePath(int processId)
  {
    try
    {
      return fileSystem
        .ResolveLinkTarget($"{ProcDirectory}/{processId}/exe", returnFinalTarget: false)?
        .FullName;
    }
    catch (Exception ex)
    {
      // Expected for a process that exited mid-scan, or one this process may not inspect.
      logger.LogDebug(ex, "Failed to read the executable path for PID {ProcessId}.", processId);
      return null;
    }
  }
}
