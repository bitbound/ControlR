using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.Serilog.Tests;

public class SerilogHostExtensionsTmpDirTests : IDisposable
{
  private const string FallbackTmpDir = "/var/tmp";
  private readonly string? _originalTmpDir = Environment.GetEnvironmentVariable("TMPDIR");
  private readonly string _realTempDir = Path.GetTempPath();

  [Fact]
  public void BootstrapSerilog_WhenTmpDirUnset_SeedsWorldWritableFallback()
  {
    Environment.SetEnvironmentVariable("TMPDIR", null);

    InvokeBootstrap();

    if (OperatingSystem.IsWindows())
    {
      Assert.Null(Environment.GetEnvironmentVariable("TMPDIR"));
      return;
    }

    Assert.Equal(FallbackTmpDir, Environment.GetEnvironmentVariable("TMPDIR"));
  }

  [Fact]
  public void BootstrapSerilog_WhenTmpDirSet_LeavesItAlone()
  {
    const string customTmpDir = "/custom-tmp-dir";
    Environment.SetEnvironmentVariable("TMPDIR", customTmpDir);

    InvokeBootstrap();

    Assert.Equal(customTmpDir, Environment.GetEnvironmentVariable("TMPDIR"));
  }

  public void Dispose()
  {
    Environment.SetEnvironmentVariable("TMPDIR", _originalTmpDir);
  }

  private void InvokeBootstrap()
  {
    var logFilePath = Path.Combine(_realTempDir, $"controlr-serilog-tests-{Guid.NewGuid()}", "LogFile.log");
    try
    {
      new ServiceCollection().BootstrapSerilog(new ConfigurationBuilder().Build(), logFilePath, TimeSpan.FromDays(7));
    }
    finally
    {
      var logsDir = Path.GetDirectoryName(logFilePath);
      if (logsDir is not null && Directory.Exists(logsDir))
      {
        Directory.Delete(logsDir, true);
      }
    }
  }
}
