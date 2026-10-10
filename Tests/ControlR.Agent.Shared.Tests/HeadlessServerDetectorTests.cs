using System.Diagnostics;
using System.IO;
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
  // Pins the grep-status-to-marker wiring of the real probe script. The shell roundtrip below only
  // proves *some* known marker comes back, so it still passes if the probe degenerates to always
  // emitting cr-display-error (which grep exit 2 makes common on a busy host, since an unreadable or
  // vanished /proc/[pid]/environ also returns 2). Here grep is shadowed with a PATH stub that exits
  // with a chosen status, so each branch is driven to its required marker without depending on this
  // machine's display state. Markers are asserted by exact equality, never as membership in a set.
  [LinuxOnlyTheory]
  [InlineData("0", false, "cr-display-found")]
  [InlineData("1", false, "cr-display-none")]
  [InlineData("2", false, "cr-display-none")]
  [InlineData("3", false, "cr-display-none")]
  [InlineData("2", true, "cr-display-error")]
  [SupportedOSPlatform("linux")]
  public async Task DisplayProbe_KnownGrepStatus_EmitsRequiredMarker(
    string grepExitStatus,
    bool forceNoReadableEnviron,
    string expectedMarker)
  {
    var marker = await RunProbeWithStubbedGrep(grepExitStatus, forceNoReadableEnviron);

    Assert.Equal(expectedMarker, marker);
  }

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

  /// <summary>
  /// Runs the exact probe arguments the detector builds, but shadows <c>grep</c> with a PATH stub that
  /// always exits with <paramref name="grepExitStatus"/>. That forces the status-to-marker wiring
  /// without depending on this machine's display state. The readability loop still runs against real
  /// /proc, so any status other than 0/1 resolves to cr-display-none when an environ is readable.
  /// Pass <paramref name="forceNoReadableEnviron"/> to source GLOBIGNORE (via BASH_ENV) so the
  /// environ glob matches nothing, simulating a probe with zero readable environ files.
  /// </summary>
  [SupportedOSPlatform("linux")]
  private static async Task<string> RunProbeWithStubbedGrep(string grepExitStatus, bool forceNoReadableEnviron)
  {
    var bashArguments = await CaptureDisplayProbeArguments();
    var stubDirectory = Path.Combine(Path.GetTempPath(), $"headless-probe-{Guid.NewGuid():N}");
    Directory.CreateDirectory(stubDirectory);

    try
    {
      var grepStubPath = Path.Combine(stubDirectory, "grep");
      await File.WriteAllTextAsync(grepStubPath, $"#!/bin/sh\nexit {grepExitStatus}\n");
      File.SetUnixFileMode(
        grepStubPath,
        UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead
        | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead
        | UnixFileMode.OtherExecute);

      var psi = new ProcessStartInfo("bash", bashArguments)
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      };

      // Prepend the stub so the bare `grep` the probe invokes resolves to it, not the system grep.
      var path = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin";
      psi.Environment["PATH"] = $"{stubDirectory}:{path}";

      if (forceNoReadableEnviron)
      {
        // GLOBIGNORE set as a plain env var is not consulted by bash -c; it must be a shell variable,
        // so source it through BASH_ENV. '*' makes /proc/[0-9]*/environ match nothing, leaving the
        // loop with zero readable environ files.
        var bashEnvPath = Path.Combine(stubDirectory, "globignore.sh");
        await File.WriteAllTextAsync(bashEnvPath, "GLOBIGNORE='*'\n");
        psi.Environment.Remove("GLOBIGNORE");
        psi.Environment["BASH_ENV"] = bashEnvPath;
      }
      else
      {
        // Readable-present path: no shell-level glob suppression. The running process's own environ
        // (/proc/<pid>/environ, same uid) is always readable, so this is deterministic regardless of
        // whatever else /proc contains.
        psi.Environment.Remove("BASH_ENV");
        psi.Environment.Remove("GLOBIGNORE");
      }

      using var process = Process.Start(psi) ?? throw new InvalidOperationException("bash failed to start.");
      var stdoutTask = process.StandardOutput.ReadToEndAsync();
      var stderrTask = process.StandardError.ReadToEndAsync();
      using var cts = new CancellationTokenSource(10_000);
      await process.WaitForExitAsync(cts.Token);
      var stdout = await stdoutTask;
      var stderr = await stderrTask;

      Assert.Equal(string.Empty, stderr.Trim());
      return stdout.Trim();
    }
    finally
    {
      Directory.Delete(stubDirectory, recursive: true);
    }
  }
}
