using System.Runtime.InteropServices;
using Bunit;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Hubs;
using ControlR.Libraries.Signalr.Client;
using ControlR.Libraries.Viewer.Common;
using ControlR.Libraries.Viewer.Common.State;
using ControlR.Web.Client.Components.RemoteDisplays;
using ControlR.Web.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;

namespace ControlR.Web.Client.Tests;

/// <summary>
/// Pins the key sequence the Input popover's "Send Keys" buttons put on the wire. Each shortcut
/// presses its modifiers in order, taps the key, and releases the modifiers in reverse, with the
/// modifier flags a browser would report on each event.
/// </summary>
public class InputPopoverKeyShortcutTests
{
  [Fact]
  public async Task CtrlShiftEsc_PressesModifiersInOrderAndReleasesInReverse()
  {
    await using var host = new Host(SystemPlatform.Windows);

    host.ClickButton("Ctrl+Shift+Esc");

    Assert.Equal(
      [
        new SentKey("Control", "ControlLeft", true, new(true, false, false, false)),
        new SentKey("Shift", "ShiftLeft", true, new(true, true, false, false)),
        new SentKey("Escape", "Escape", true, new(true, true, false, false)),
        new SentKey("Escape", "Escape", false, new(true, true, false, false)),
        new SentKey("Shift", "ShiftLeft", false, new(true, false, false, false)),
        new SentKey("Control", "ControlLeft", false, KeyEventModifiersDto.None),
      ],
      host.SentKeys);
  }

  [Fact]
  public async Task LinuxDevice_ShowsSuperInsteadOfWindowsShortcuts()
  {
    await using var host = new Host(SystemPlatform.Linux);

    var labels = host.ButtonLabels();

    Assert.Contains("Super", labels);
    Assert.Contains("Alt+Tab", labels);
    Assert.DoesNotContain("Win", labels);
    Assert.DoesNotContain("Win+R", labels);
  }

  [Fact]
  public async Task ViewOnlyMode_SendsNothing()
  {
    await using var host = new Host(SystemPlatform.Windows, isViewOnly: true);

    host.ClickButton("Win+R");

    Assert.Empty(host.SentKeys);
  }

  [Fact]
  public async Task WinR_SendsMetaChordWithCurrentInputMode()
  {
    await using var host = new Host(SystemPlatform.Windows, inputMode: KeyboardInputMode.Physical);

    host.ClickButton("Win+R");

    Assert.Equal(
      [
        new SentKey("Meta", "MetaLeft", true, new(false, false, false, true)),
        new SentKey("r", "KeyR", true, new(false, false, false, true)),
        new SentKey("r", "KeyR", false, new(false, false, false, true)),
        new SentKey("Meta", "MetaLeft", false, KeyEventModifiersDto.None),
      ],
      host.SentKeys);
    Assert.All(host.SentInputModes, mode => Assert.Equal(KeyboardInputMode.Physical, mode));
  }

  private static DeviceResponseDto CreateDevice(SystemPlatform platform)
  {
    return new DeviceResponseDto(
      Name: "WS-01",
      AgentVersion: "1.0.0",
      CpuUtilization: 0,
      Id: Guid.NewGuid(),
      Is64Bit: true,
      IsOnline: true,
      LastSeen: DateTimeOffset.UtcNow,
      OsArchitecture: Architecture.X64,
      Platform: platform,
      ProcessorCount: 8,
      ConnectionId: "connection",
      OsDescription: "OS",
      TenantId: Guid.NewGuid(),
      TotalMemory: 16,
      TotalStorage: 512,
      UsedMemory: 8,
      UsedStorage: 256,
      CurrentUsers: [],
      MacAddresses: [],
      PublicIpV4: "1.2.3.4",
      PublicIpV6: "::1",
      LocalIpV4: "10.0.0.1",
      LocalIpV6: "::1",
      Drives: [],
      IsOutdated: false);
  }

  private sealed record SentKey(string Key, string? Code, bool IsPressed, KeyEventModifiersDto Modifiers);

  private sealed class Host : IAsyncDisposable
  {
    private readonly BunitContext _ctx = new();
    private readonly IRenderedComponent<InputPopover> _cut;

    public Host(SystemPlatform platform, bool isViewOnly = false, KeyboardInputMode inputMode = KeyboardInputMode.Auto)
    {
      var deviceState = new Mock<IDeviceState> { DefaultValue = DefaultValue.Mock };
      deviceState.Setup(x => x.TryGetCurrentDevice()).Returns(CreateDevice(platform));

      var remoteControlState = new Mock<IRemoteControlState> { DefaultValue = DefaultValue.Mock };
      remoteControlState.SetupGet(x => x.IsViewOnlyEnabled).Returns(isViewOnly);
      remoteControlState.SetupGet(x => x.KeyboardInputMode).Returns(inputMode);

      var stream = new Mock<IViewerRemoteControlStream> { DefaultValue = DefaultValue.Mock };
      stream.SetupGet(x => x.IsConnected).Returns(true);
      stream
        .Setup(x => x.SendKeyEvent(
          It.IsAny<string>(),
          It.IsAny<string?>(),
          It.IsAny<bool>(),
          It.IsAny<KeyboardInputMode>(),
          It.IsAny<KeyEventModifiersDto>(),
          It.IsAny<CancellationToken>()))
        .Callback((string key, string? code, bool isPressed, KeyboardInputMode mode, KeyEventModifiersDto modifiers, CancellationToken _) =>
        {
          SentKeys.Add(new SentKey(key, code, isPressed, modifiers));
          SentInputModes.Add(mode);
        })
        .Returns(Task.CompletedTask);

      _ctx.Services.AddMudServices();
      _ctx.Services.AddSingleton(new Mock<IClipboardManager>().Object);
      _ctx.Services.AddSingleton(new Mock<IDeviceAccessPermissionsState> { DefaultValue = DefaultValue.Mock }.Object);
      _ctx.Services.AddSingleton(deviceState.Object);
      _ctx.Services.AddSingleton<ILogger<InputPopover>>(NullLogger<InputPopover>.Instance);
      _ctx.Services.AddSingleton(remoteControlState.Object);
      _ctx.Services.AddSingleton(stream.Object);
      _ctx.Services.AddSingleton(new Mock<IHubConnection<IViewerHub>> { DefaultValue = DefaultValue.Mock }.Object);
      _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

      _ctx.Render<MudPopoverProvider>();
      _cut = _ctx.Render<InputPopover>();
    }

    public List<KeyboardInputMode> SentInputModes { get; } = [];

    public List<SentKey> SentKeys { get; } = [];

    public IReadOnlyList<string> ButtonLabels()
    {
      return [.. _cut.FindAll("button").Select(x => x.TextContent.Trim())];
    }

    public void ClickButton(string label)
    {
      _cut.FindAll("button").Single(x => x.TextContent.Trim() == label).Click();
    }

    public ValueTask DisposeAsync()
    {
      return _ctx.DisposeAsync();
    }
  }
}
