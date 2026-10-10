using System.Diagnostics;
using System.Runtime.Versioning;
using ControlR.Agent.Shared.Services.Linux;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services.Processes;
using ControlR.Libraries.TestingUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Shared.Tests;

/// <summary>
/// Pins the DISPLAY probe in headless detection (#318). The old <c>ps -eo env</c> scrape was
/// rejected by procps ("env" is not a format specifier), and because GetProcessOutput leaves the
/// child's stderr inherited, the usage text landed in the agent log untagged. The replacement
/// greps DISPLAY from /proc/[0-9]*/environ instead.
/// </summary>
public class HeadlessServerDetectorTests
{
  [LinuxOnlyFact]
  [SupportedOSPlatform("linux")]
  public async Task DisplayProbe_WhenRunByARealShell_WritesNoPsUsageTextToStderr()
  {
    var bashArguments = await CaptureDisplayProbeArguments();

    // Re-run the exact arguments the detector hands to GetProcessOutput, through the same
    // bash/-c shape, but with stderr redirected so it can be asserted instead of leaking.
    var psi = new ProcessStartInfo("bash", bashArguments)
    {
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };
    using var process = Process.Start(psi) ?? throw new InvalidOperationException("bash failed to start.");
    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();
    using var cts = new CancellationTokenSource(10_000);
    await process.WaitForExitAsync(cts.Token);
    // Whether "found" is printed depends on what is running on this machine, so stdout is drained
    // but not asserted. Stderr must stay silent either way.
    await stdoutTask;
    var stderr = await stderrTask;

    Assert.DoesNotContain("unknown user-defined format specifier", stderr, StringComparison.Ordinal);
    Assert.DoesNotContain("Usage:", stderr, StringComparison.Ordinal);
  }

  [Fact]
  public async Task IsHeadlessServer_WhenDisplayProbeFindsVariable_ReturnsNotHeadless()
  {
    var bashCalls = new List<string>();
    var processManager = new Mock<IProcessManager>();
    processManager
      .Setup(x => x.GetProcessOutput(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(string.Empty));
    processManager
      .Setup(x => x.GetProcessOutput("bash", It.IsAny<string>(), It.IsAny<int>()))
      .Callback<string, string, int>((_, arguments, __) => bashCalls.Add(arguments))
      .ReturnsAsync(Result.Ok("found"));

    var detector = new HeadlessServerDetector(
      processManager.Object,
      NullLogger<HeadlessServerDetector>.Instance);

    var isHeadless = await detector.IsHeadlessServer();

    Assert.False(isHeadless);
    Assert.NotEmpty(bashCalls);
    Assert.All(bashCalls, arguments => Assert.DoesNotContain("ps -eo env", arguments, StringComparison.Ordinal));
  }

  private static async Task<string> CaptureDisplayProbeArguments()
  {
    string? capturedArguments = null;
    var processManager = new Mock<IProcessManager>();
    processManager
      .Setup(x => x.GetProcessOutput(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(string.Empty));
    processManager
      .Setup(x => x.GetProcessOutput("bash", It.IsAny<string>(), It.IsAny<int>()))
      .Callback<string, string, int>((_, arguments, __) => capturedArguments = arguments)
      .ReturnsAsync(Result.Fail<string>("Captured only. The test body runs the real command."));

    var detector = new HeadlessServerDetector(
      processManager.Object,
      NullLogger<HeadlessServerDetector>.Instance);
    await detector.IsHeadlessServer();

    return capturedArguments ?? throw new InvalidOperationException("The detector never invoked bash.");
  }
}
