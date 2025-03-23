using Bitbound.SimpleMessenger;
using ControlR.Devices.Native.Services;
using ControlR.Libraries.ScreenCapture;
using ControlR.Libraries.ScreenCapture.Helpers;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Streamer.Options;
using ControlR.Streamer.Services;
using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Text.Json;
using Xunit.Abstractions;
namespace ControlR.Streamer.Tests;

public class DesktopCapturerTests
{
  private readonly FakeHostApplicationLifetime _appLifetime;
  private readonly InputSimulatorWindows _inputSim;
  private readonly BitmapUtility _bitmapUtility;
  private readonly DesktopCapturer _capturer;
  private readonly Delayer _delayer;
  private readonly DxOutputGenerator _dxOutputs;
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
  private readonly XunitLogger<DesktopCapturer> _logger;
  private readonly MemoryProvider _memoryProvider;
  private readonly ScreenGrabber _screenGrabber;
  private readonly StartupOptions _startupOptions;
  private readonly FakeTimeProvider _timeProvider;
  private readonly Win32Interop _win32Interop;

  public DesktopCapturerTests(ITestOutputHelper outputHelper)
  {
    _timeProvider = new FakeTimeProvider();
    _logger = new XunitLogger<DesktopCapturer>(outputHelper);
    _bitmapUtility = new BitmapUtility();
    _dxOutputs = new DxOutputGenerator(new XunitLogger<DxOutputGenerator>(outputHelper));
    _memoryProvider = new MemoryProvider();
    _win32Interop = new Win32Interop(new XunitLogger<Win32Interop>(outputHelper));
    _delayer = new Delayer();
    _appLifetime = new FakeHostApplicationLifetime(_timeProvider);
    _inputSim = new InputSimulatorWindows(_win32Interop, new XunitLogger<InputSimulatorWindows>(outputHelper));

    var sessionId = Guid.NewGuid();
    var accessKey = RandomGenerator.CreateAccessToken();

    _startupOptions = new StartupOptions()
    {
      NotifyUser = false,
      ServerOrigin = new Uri("http://localhost:5120"),
      SessionId = sessionId,
      ViewerName = "Test Viewer",
      WebSocketUri = new Uri($"ws://localhost:5120/relay/{sessionId}/{accessKey}")
    };

    _screenGrabber = new ScreenGrabber(
      timeProvider: _timeProvider,
      bitmapUtility: _bitmapUtility,
      dxOutputGenerator: _dxOutputs,
      new XunitLogger<ScreenGrabber>(outputHelper));

    _capturer = new DesktopCapturer(
      timeProvider: _timeProvider,
      messenger: WeakReferenceMessenger.Default,
      screenGrabber: _screenGrabber,
      bitmapUtility: _bitmapUtility,
      memoryProvider: _memoryProvider,
      win32Interop: _win32Interop,
      delayer: _delayer,
      appLifetime: _appLifetime,
      startupOptions: new OptionsWrapper<StartupOptions>(_startupOptions),
      logger: _logger);
  }

  [Fact(Skip = "Manual")]
  public async Task ManualTests()
  {
    var displays = _capturer.GetDisplays();

    var primaryDisplay = displays.First(x => x.IsPrimary);
    var secondaryDisplay = displays.First(x => !x.IsPrimary);

    await _capturer.ChangeDisplays(primaryDisplay.DisplayId);

    var abs = await _capturer.ConvertPercentageLocationToAbsolute(0.5, 0.5);
    _logger.LogInformation("Absolute: {Abs}", JsonSerializer.Serialize(abs, _jsonOptions));

    _inputSim.MovePointer(abs.X, abs.Y, Libraries.Shared.Dtos.StreamerDtos.MovePointerType.Absolute);
    
    var virtualScreen = _screenGrabber.GetVirtualScreenBounds();
  }
}
