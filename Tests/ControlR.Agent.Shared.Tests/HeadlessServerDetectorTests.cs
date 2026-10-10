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
/// greps DISPLAY from /proc/[0-9]*/environ and prints a self-reporting marker, so a probe that
/// could not run is distinguishable from one that ran and found no display. The marker literals
/// below are written out instead of referencing detector constants on purpose: they are the wire
/// contract between the probe script and the branching logic, and the tests must fail if the two
/// sides ever diverge.
/// </summary>
public class HeadlessServerDetectorTests
{
  [LinuxOnlyFact]
  [SupportedOSPlatform("linux")]
  public async Task DisplayProbe_WhenRunByARealShell_EmitsOneKnownMarkerAndNoStderr()
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
    var stdout = await stdoutTask;
    var stderr = await stderrTask;

    // GetProcessOutput inherits the child's stderr into the agent's own output, so anything
    // written here is the untagged-log-spam symptom of the original defect. Whether the probe
    // finds a display depends on this machine; silence on stderr is unconditional.
    Assert.Equal(string.Empty, stderr.Trim());

    // The probe must self-report. Empty stdout is indistinguishable from a clean negative to
    // GetProcessOutput, which ignores the child's exit code, so a marker is the only evidence
    // the probe actually ran.
    Assert.Contains(
      stdout.Trim(),
      new[] { "cr-display-found", "cr-display-none", "cr-display-error" });
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
      .ReturnsAsync(Result.Ok("cr-display-found"));

    var detector = new HeadlessServerDetector(
      processManager.Object,
      NullLogger<HeadlessServerDetector>.Instance);

    var isHeadless = await detector.IsHeadlessServer();

    Assert.False(isHeadless);
    Assert.NotEmpty(bashCalls);
    Assert.All(bashCalls, arguments => Assert.DoesNotContain("ps -eo env", arguments, StringComparison.Ordinal));
  }

  [Fact]
  public async Task IsHeadlessServer_WhenProbeEmitsErrorMarker_FailsClosedBeforeFallbackChecks()
  {
    // grep missing or /proc stripped: the probe says it could not run. That is not a negative
    // observation, so the detector must report headless without treating the broken probe as
    // evidence and without continuing as if the probe had answered "none".
    var (detector, processManager) = CreateDetectorWithProbeResult(Result.Ok("cr-display-error"));

    var isHeadless = await detector.IsHeadlessServer();

    Assert.True(isHeadless);
    processManager
      .Verify(x => x.GetProcessOutput("dpkg", It.IsAny<string>(), It.IsAny<int>()), Times.Never);
  }

  [Fact]
  public async Task IsHeadlessServer_WhenProbeEmitsNoneMarker_ContinuesToFallbackChecks()
  {
    // The clean negative: the probe ran and found no DISPLAY anywhere. Detection must proceed
    // to the remaining signals instead of short-circuiting like a broken probe.
    var (detector, processManager) = CreateDetectorWithProbeResult(Result.Ok("cr-display-none"));

    var isHeadless = await detector.IsHeadlessServer();

    Assert.True(isHeadless);
    processManager
      .Verify(x => x.GetProcessOutput("dpkg", It.IsAny<string>(), It.IsAny<int>()), Times.Once);
  }

  [Fact]
  public async Task IsHeadlessServer_WhenProbeEmitsNothingOnStdout_FailsClosedBeforeFallbackChecks()
  {
    // Non-GNU grep rejecting -z, a script killed early, or bash writing its markers nowhere:
    // no known marker means the probe result carries no information, so it must not be read
    // as a clean negative.
    var (detector, processManager) = CreateDetectorWithProbeResult(Result.Ok(string.Empty));

    var isHeadless = await detector.IsHeadlessServer();

    Assert.True(isHeadless);
    processManager
      .Verify(x => x.GetProcessOutput("dpkg", It.IsAny<string>(), It.IsAny<int>()), Times.Never);
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

  /// <summary>
  /// Detector whose pgrep probes return no processes and whose dpkg fallback fails, so the only
  /// signal available is the mocked DISPLAY probe result.
  /// </summary>
  private static (HeadlessServerDetector Detector, Mock<IProcessManager> ProcessManager) CreateDetectorWithProbeResult(
    Result<string> bashProbeResult)
  {
    var processManager = new Mock<IProcessManager>();
    processManager
      .Setup(x => x.GetProcessOutput(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(string.Empty));
    processManager
      .Setup(x => x.GetProcessOutput("dpkg", It.IsAny<string>(), It.IsAny<int>()))
      .ReturnsAsync(Result.Fail<string>("Simulated host without dpkg evidence."));
    processManager
      .Setup(x => x.GetProcessOutput("bash", It.IsAny<string>(), It.IsAny<int>()))
      .ReturnsAsync(bashProbeResult);

    var detector = new HeadlessServerDetector(
      processManager.Object,
      NullLogger<HeadlessServerDetector>.Instance);

    return (Detector: detector, ProcessManager: processManager);
  }
}
