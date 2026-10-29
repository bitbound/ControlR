using System.Runtime.CompilerServices;

namespace ControlR.Agent.Common.Tests;

internal static class TempDirStability
{
  // The agent host bootstrap seeds TMPDIR process-wide on Unix (SerilogHostExtensions). Because
  // Path.GetTempPath() re-reads TMPDIR on every call, a test that builds a path before a parallel
  // test seeds it and compares against a path built after would see /tmp against /var/tmp. Pin the
  // value to the same one the production seed uses before any test runs, which also makes the seed
  // a no-op.
  [ModuleInitializer]
  internal static void PinUnixTempDir()
  {
    if (!OperatingSystem.IsWindows() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TMPDIR")))
    {
      Environment.SetEnvironmentVariable("TMPDIR", "/var/tmp");
    }
  }
}
