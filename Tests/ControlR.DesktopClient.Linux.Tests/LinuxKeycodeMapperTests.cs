using ControlR.Libraries.NativeInterop.Linux;
using Xunit;

namespace ControlR.DesktopClient.Linux.Tests;

public class LinuxKeycodeMapperTests
{
  [Fact]
  public void BrowserCodeToLinuxKeycode_Respects_Canonical_Mappings()
  {
    // A canonical KeyboardEvent.code should still map correctly
    Assert.Equal(30, LinuxKeycodeMapper.BrowserCodeToLinuxKeycode("KeyA", null));
    Assert.Equal(3, LinuxKeycodeMapper.BrowserCodeToLinuxKeycode("Digit2", null));
  }

  [Fact]
  public void BrowserCodeToLinuxKeycode_Returns_Backspace_For_Canonical_And_KeyName()
  {
    Assert.Equal(14, LinuxKeycodeMapper.BrowserCodeToLinuxKeycode("Backspace", null));
    Assert.Equal(14, LinuxKeycodeMapper.BrowserCodeToLinuxKeycode(null, "backspace"));
  }

  [Fact]
  public void BrowserCodeToLinuxKeycode_Returns_Enter_For_Canonical_And_KeyName()
  {
    // Code-first (canonical code)
    Assert.Equal(28, LinuxKeycodeMapper.BrowserCodeToLinuxKeycode("Enter", null));
    // Fallback using key name
    Assert.Equal(28, LinuxKeycodeMapper.BrowserCodeToLinuxKeycode(null, "enter"));
    Assert.Equal(28, LinuxKeycodeMapper.BrowserCodeToLinuxKeycode(null, "Enter"));
  }

  [Fact]
  public void BrowserCodeToLinuxKeycode_Returns_MinusOne_For_Unknown_Or_Null()
  {
    Assert.Equal(-1, LinuxKeycodeMapper.BrowserCodeToLinuxKeycode(null, null));
    Assert.Equal(-1, LinuxKeycodeMapper.BrowserCodeToLinuxKeycode(string.Empty, string.Empty));
    Assert.Equal(-1, LinuxKeycodeMapper.BrowserCodeToLinuxKeycode("UnknownCode", "UnknownKey"));
  }
}
