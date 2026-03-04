#pragma warning disable CS0067 
using System.ComponentModel;
using Avalonia.Layout;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using System.Collections.Generic;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Viewer.Avalonia.ViewModels.Fakes;
internal class RemoteDisplayViewModelFake : IRemoteDisplayViewModel
{
  public event EventHandler? FrameQueued;
  public event PropertyChangedEventHandler? PropertyChanged;

  public CursorChangedDto? ActiveCursor => null;
  public SKBitmap? CompositedFrame => null;
  public IAsyncRelayCommand DisconnectCommand => new AsyncRelayCommand(() => Task.CompletedTask);
  public ObservableCollection<DisplayLayoutItem> DisplayItems { get; } =
  [
    new DisplayLayoutItem(
      new DisplayDto
      {
        DisplayId = "display-0",
        Height = 1080,
        Index = 0,
        IsPrimary = true,
        Left = 0,
        Name = "Display 0",
        ScaleFactor = 1,
        Top = 0,
        Width = 1920
      },
      _ => Task.CompletedTask)
    {
      IsSelected = true,
      LayoutLeft = 0,
      LayoutTop = 20,
      LayoutWidth = 200,
      LayoutHeight = 100,
    },
    new DisplayLayoutItem(
      new DisplayDto
      {
        DisplayId = "display-1",
        Height = 1080,
        Index = 1,
        IsPrimary = false,
        Left = 1920,
        Name = "Display 1",
        ScaleFactor = 1,
        Top = 0,
        Width = 2560
      },
      _ => Task.CompletedTask)
    {
      LayoutLeft = 200,
      LayoutTop = 0,
      LayoutWidth = 80,
      LayoutHeight = 140,
    }
  ];
  public bool HasMetricsData => true;
  public bool HasMultipleDisplays => true;
  public bool IsAutoPanEnabled { get; set; } = true;
  public bool IsBlockInputToggleEnabled => true;
  public bool IsBlockUserInputEnabled { get; set; }
  public bool IsFitViewMode { get; set; }
  public bool IsKeyboardInputAuto { get; set; } = true;
  public bool IsKeyboardInputPhysical { get; set; }
  public bool IsKeyboardInputVirtual { get; set; }
  public bool IsMetricsEnabled { get; set; } = true;
  public bool IsPrivacyScreenEnabled { get; set; }
  public bool IsScaleControlsVisible => IsScaleViewMode;
  public bool IsScaleViewMode { get; set; }
  public bool IsStretchViewMode { get; set; } = true;
  public bool IsViewOnlyEnabled { get; set; }
  public ILogger<RemoteDisplayViewModel> Logger { get; } = new LoggerFactory().CreateLogger<RemoteDisplayViewModel>();
  public double MaxRendererScale => 3;
  public IReadOnlyDictionary<string, string> MetricsExtraData { get; } =
    new Dictionary<string, string>
    {
      ["Thread ID"] = "22",
      ["Thread Desktop Name"] = "Default",
      ["Input Desktop Name"] = "Default"
    };
  public double MetricsFps => 64.33;
  public TimeSpan MetricsLatency => TimeSpan.FromMilliseconds(42.1);
  public double MetricsMbpsIn => 0;
  public double MetricsMbpsOut => 0;
  public string MetricsMode => "DirectX";
  public double MinRendererScale => 0.2;
  public double RendererHeight => double.NaN;
  public HorizontalAlignment RendererHorizontalAlignment => HorizontalAlignment.Stretch;
  public double RendererScale { get; set; } = 1;
  public VerticalAlignment RendererVerticalAlignment => VerticalAlignment.Stretch;
  public double RendererWidth => double.NaN;
  public IAsyncRelayCommand<DisplayLayoutItem?> SelectDisplayCommand =>
    new AsyncRelayCommand<DisplayLayoutItem?>(_ => Task.CompletedTask);
  public double SelectedDisplayHeight { get; }
  public double SelectedDisplayWidth { get; }
  public bool ShowPrivacyScreenToggle => true;
  public bool ShowWindowsInputControls => true;
  public ViewMode ViewMode => ViewMode.Fit;

  public DisposableValue<SKBitmap?> AcquireCompositedFrame()
  {
    return new DisposableValue<SKBitmap?>(null, () => { });
  }

  public void Dispose()
  {
    // No-op;
  }

  public Task InvokeCtrlAltDel()
  {
    return Task.CompletedTask;
  }

  public Task RequestClipboardText()
  {
    return Task.CompletedTask;
  }

  public Task SendClipboardText(string text)
  {
    return Task.CompletedTask;
  }

  public Task SendKeyboardStateReset()
  {
    return Task.CompletedTask;
  }

  public Task SendKeyEvent(string key, string code, bool isPressed, KeyEventModifiersDto modifiers)
  {
    return Task.CompletedTask;
  }

  public Task SendMouseButtonEvent(int button, bool isPressed, double percentX, double percentY)
  {
    return Task.CompletedTask;
  }

  public Task SendPointerMove(double percentX, double percentY)
  {
    return Task.CompletedTask;
  }

  public Task SendWheelScroll(double percentX, double percentY, double scrollY, double scrollX)
  {
    return Task.CompletedTask;
  }

  public Task TypeClipboardText(string text)
  {
    return Task.CompletedTask;
  }
}
