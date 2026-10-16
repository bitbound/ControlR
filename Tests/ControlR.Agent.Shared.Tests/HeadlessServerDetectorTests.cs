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
/// Pins the DISPLAY probe in headless detection. The old <c>ps -eo env</c> scrape was
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
  // emitting cr-display-error. Here grep is shadowed with a PATH stub that exits with the status
  // under test, driving each branch to its required marker without depending on this machine's
  // display state. Markers are asserted by exact equality, never as membership in a set.
  [LinuxOnlyTheory]
  // grep matched DISPLAY.
  [InlineData("0", "cr-display-found")]
  // grep ran and DISPLAY is absent from every environ it could read.
  [InlineData("1", "cr-display-none")]
  // A partial scan (an environ owned by another user, or a PID that vanished): the unreadable
  // files are where another user's DISPLAY would be, so no negative can be claimed from it.
  [InlineData("2", "cr-display-error")]
  // Any other status means grep could not answer.
  [InlineData("3", "cr-display-error")]
  // Launch failures: grep not executable (126) or missing from PATH (127).
  [InlineData("126", "cr-display-error")]
  [InlineData("127", "cr-display-error")]
  [SupportedOSPlatform("linux")]
  public async Task DisplayProbe_KnownGrepStatus_EmitsRequiredMarker(
    string grepExitStatus,
    string expectedMarker)
  {
    var marker = await RunProbeWithStubbedGrep(grepExitStatus);

    Assert.Equal(expectedMarker, marker);
  }

  [LinuxOnlyFact]
  [SupportedOSPlatform("linux")]
  public async Task DisplayProbe_WhenGrepIsMissingFromPath_EmitsErrorMarker()
  {
    // No stub, no system grep: bash reports 127 and the probe must declare itself broken instead
    // of resolving to a confident negative.
    var marker = await RunProbeWithGrepMissing();

    Assert.Equal("cr-display-error", marker);
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
    var stdout = await RunAndCapture(psi);

    // The probe must self-report. Empty stdout is indistinguishable from a clean negative to
    // GetProcessOutput, which ignores the child's exit code, so a marker is the only evidence
    // the probe actually ran.
    Assert.Contains(
      stdout,
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
  public async Task IsHeadlessServer_WhenProbeEmitsErrorMarker_FailsClosed()
  {
    // grep missing or /proc stripped: the probe says it could not run. That is not a negative
    // observation, so the detector must report headless without treating the broken probe as
    // evidence and without continuing as if the probe had answered "none".
    var (detector, _) = CreateDetectorWithProbeResult(Result.Ok("cr-display-error"));

    var isHeadless = await detector.IsHeadlessServer();

    Assert.True(isHeadless);
  }

  [Fact]
  public async Task IsHeadlessServer_WhenProbeEmitsNoneMarker_ReturnsHeadless()
  {
    // The clean negative: the probe ran and found no DISPLAY anywhere, so the host is headless.
    // The former ubuntu-server dpkg fallback could not change that outcome (every path past the
    // none marker returned headless) and its IsSuccess check was exit-code blind, so it is gone;
    // dpkg must not be invoked at all.
    var (detector, processManager) = CreateDetectorWithProbeResult(Result.Ok("cr-display-none"));

    var isHeadless = await detector.IsHeadlessServer();

    Assert.True(isHeadless);
    processManager
      .Verify(x => x.GetProcessOutput("dpkg", It.IsAny<string>(), It.IsAny<int>()), Times.Never);
  }

  [Fact]
  public async Task IsHeadlessServer_WhenProbeEmitsNothingOnStdout_FailsClosed()
  {
    // A script killed early, or bash writing its markers nowhere: no known marker means the probe
    // result carries no information, so it must not be read as a clean negative.
    var (detector, _) = CreateDetectorWithProbeResult(Result.Ok(string.Empty));

    var isHeadless = await detector.IsHeadlessServer();

    Assert.True(isHeadless);
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
  /// Detector whose pgrep probes return no processes, so the only signal available is the mocked
  /// DISPLAY probe result.
  /// </summary>
  private static (HeadlessServerDetector Detector, Mock<IProcessManager> ProcessManager) CreateDetectorWithProbeResult(
    Result<string> bashProbeResult)
  {
    var processManager = new Mock<IProcessManager>();
    processManager
      .Setup(x => x.GetProcessOutput(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
      .ReturnsAsync(Result.Ok(string.Empty));
    processManager
      .Setup(x => x.GetProcessOutput("bash", It.IsAny<string>(), It.IsAny<int>()))
      .ReturnsAsync(bashProbeResult);

    var detector = new HeadlessServerDetector(
      processManager.Object,
      NullLogger<HeadlessServerDetector>.Instance);

    return (Detector: detector, ProcessManager: processManager);
  }

  /// <summary>
  /// Starts bash, waits for it under a timeout, and returns trimmed stdout. GetProcessOutput
  /// inherits the child's stderr into the agent's own output, so anything on stderr is the
  /// untagged-log-spam symptom of the original defect: silence there is asserted unconditionally.
  /// </summary>
  private static async Task<string> RunAndCapture(ProcessStartInfo psi)
  {
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

  /// <summary>
  /// Runs the exact probe arguments with a PATH that contains no grep. An unset PATH is not
  /// enough: bash falls back to its default search path, which does contain grep.
  /// </summary>
  [SupportedOSPlatform("linux")]
  private static async Task<string> RunProbeWithGrepMissing()
  {
    var bashArguments = await CaptureDisplayProbeArguments();
    var emptyDirectory = Path.Combine(Path.GetTempPath(), $"headless-probe-empty-{Guid.NewGuid():N}");
    Directory.CreateDirectory(emptyDirectory);

    try
    {
      var psi = new ProcessStartInfo("bash", bashArguments)
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      };
      psi.Environment["PATH"] = emptyDirectory;
      psi.Environment.Remove("BASH_ENV");
      psi.Environment.Remove("GLOBIGNORE");

      return await RunAndCapture(psi);
    }
    finally
    {
      Directory.Delete(emptyDirectory, recursive: true);
    }
  }

  /// <summary>
  /// Runs the exact probe arguments the detector builds, but shadows <c>grep</c> with a PATH stub.
  /// The stub exits with <paramref name="grepExitStatus"/>, which forces the
  /// status-to-marker wiring without depending on this machine's display state.
  /// </summary>
  [SupportedOSPlatform("linux")]
  private static async Task<string> RunProbeWithStubbedGrep(string grepExitStatus)
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
      psi.Environment.Remove("BASH_ENV");
      psi.Environment.Remove("GLOBIGNORE");

      return await RunAndCapture(psi);
    }
    finally
    {
      Directory.Delete(stubDirectory, recursive: true);
    }
  }
}
