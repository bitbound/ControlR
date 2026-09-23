using ControlR.DesktopClient.Windows.Services;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.DesktopClient.Windows.Tests;

public class InputSimulatorWindowsTests
{
  [Fact]
  public void ShouldUseTextEvent_Keeps_Non_Physical_Modes()
  {
    Assert.True(InputSimulatorWindows.ShouldUseTextEvent("!", string.Empty, KeyboardInputMode.Auto, KeyEventModifiersDto.None));
    Assert.True(InputSimulatorWindows.ShouldUseTextEvent("!", string.Empty, KeyboardInputMode.Virtual, KeyEventModifiersDto.None));
  }

  [Theory]
  [InlineData("Enter")]
  [InlineData("\t")]
  public void ShouldUseTextEvent_Rejects_Non_Printable_Keys(string key)
  {
    Assert.False(InputSimulatorWindows.ShouldUseTextEvent(key, string.Empty, KeyboardInputMode.Physical, KeyEventModifiersDto.None));
  }

  [Fact]
  public void ShouldUseTextEvent_Rejects_Shortcut_Modifiers()
  {
    var modifiers = new KeyEventModifiersDto(Control: true, Shift: false, Alt: false, Meta: false);

    Assert.False(InputSimulatorWindows.ShouldUseTextEvent("c", string.Empty, KeyboardInputMode.Physical, modifiers));
  }

  [Fact]
  public void ShouldUseTextEvent_Uses_Physical_Key_When_Code_Is_Present()
  {
    Assert.False(InputSimulatorWindows.ShouldUseTextEvent("!", "Digit1", KeyboardInputMode.Physical, KeyEventModifiersDto.None));
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void ShouldUseTextEvent_Uses_Text_For_Physical_Mode_Without_Code(string code)
  {
    Assert.True(InputSimulatorWindows.ShouldUseTextEvent("!", code, KeyboardInputMode.Physical, KeyEventModifiersDto.None));
  }
}