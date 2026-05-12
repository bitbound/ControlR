using ControlR.Libraries.TestingUtilities;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Logging;
using Xunit;
using Tmds.DBus.Protocol;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.DesktopClient.Common.Options;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.FileSystem;
using Castle.Core.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.DesktopClient.Linux.Tests;

public class XdgDesktopPortalTests : IDisposable
{
  private readonly XdgDesktopPortal _desktopPortal;
  private readonly FakeFileSystem _fileSystem;
  private readonly ILogger<XdgDesktopPortal> _logger;
  private readonly OptionsMonitorWrapper<DesktopClientOptions> _options;
  private readonly ITestOutputHelper _outputHelper;
  private readonly FileSystem _realFileSystem;
  private readonly CancellationToken _testCancellationToken;

  public XdgDesktopPortalTests(ITestOutputHelper outputHelper)
  {
    var loggerFactory = LoggerFactory.Create(builder =>
    {
      builder.AddProvider(new XunitLoggerProvider(outputHelper));
      builder.SetMinimumLevel(LogLevel.Debug);
    });
    _outputHelper = outputHelper;
    _logger = loggerFactory.CreateLogger<XdgDesktopPortal>();
    _fileSystem = new FakeFileSystem();
    _options = new OptionsMonitorWrapper<DesktopClientOptions>(new DesktopClientOptions());
    _desktopPortal = new XdgDesktopPortal(_fileSystem, _fileSystem, _options, _logger);
    _realFileSystem = new FileSystem(NullLoggerFactory.Instance.CreateLogger<FileSystem>());
    _testCancellationToken = TestContext.Current.CancellationToken;
  }

  [WaylandOnlyFact]
  public async Task CanConnectToDBus()
  {
    await _desktopPortal.Initialize(cancellationToken: _testCancellationToken);
    var sessionHandle = await _desktopPortal.GetRemoteDesktopSessionHandle(_testCancellationToken);
    Assert.NotNull(sessionHandle);
  }

  [WaylandOnlyFact]
  public async Task CanGetPipeWireConnection()
  {
    var connection = await _desktopPortal.GetPipeWireConnection();
    Assert.NotNull(connection);
    Assert.False(connection.Value.Fd.IsInvalid);
    Assert.False(string.IsNullOrEmpty(connection.Value.SessionHandle));
  }

  public void Dispose()
  {
    _desktopPortal.Dispose();
    GC.SuppressFinalize(this);
  }

  [WaylandOnlyFact]
  public async Task GetClipboardText()
  {
    var whereResult = await _realFileSystem.ResolveFilePath("wl-copy");
    if (!whereResult.IsSuccess)
    {
      _outputHelper.WriteLine("wl-copy not found in PATH, skipping clipboard test.");
      return;
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, _testCancellationToken);

    await Process.Start("wl-copy", "Test").WaitForExitAsync(linkedToken.Token);

    await _desktopPortal.Initialize();
    await Task.Delay(500);
    var result = await _desktopPortal.GetClipboardText(linkedToken.Token);
    Assert.Equal("Test", result);
  }

  [WaylandOnlyFact]
  public async Task SetGetClipboardText()
  {
    var whereResult = await _realFileSystem.ResolveFilePath("wl-paste");
    if (!whereResult.IsSuccess)
    {
      _outputHelper.WriteLine("wl-paste not found in PATH, skipping clipboard test.");
      return;
    }

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, _testCancellationToken);

    await _desktopPortal.Initialize();

    var testText = "Hello, World!";
    await _desktopPortal.SetClipboardText(testText, cts.Token);

    var psi = new ProcessStartInfo("wl-paste", "--no-newline")
    {
      RedirectStandardOutput = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };
    var process = Process.Start(psi);
    Assert.NotNull(process);

    var output = await process.StandardOutput.ReadToEndAsync(linkedToken.Token);
    await process.WaitForExitAsync(linkedToken.Token);
    Assert.Equal(testText, output);
  }
}
