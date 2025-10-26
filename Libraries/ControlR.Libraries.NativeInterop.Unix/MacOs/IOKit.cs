#pragma warning disable IDE1006 // Naming Styles
using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.MacOs;

public static class IOKit
{
  // IOPMAssertionLevel values
  public const uint kIOPMAssertionLevelOff = 0;
  public const uint kIOPMAssertionLevelOn = 255;
  public const string kIOPMAssertionTypePreventSystemSleep = "PreventSystemSleep";

  // IOPMAssertion types
  public const string kIOPMAssertionTypePreventUserIdleDisplaySleep = "PreventUserIdleDisplaySleep";
  public const string kIOPMAssertionTypePreventUserIdleSystemSleep = "PreventUserIdleSystemSleep";

  // IOReturn codes
  public const uint kIOReturnSuccess = 0;

  private const string IOKitFramework = "/System/Library/Frameworks/IOKit.framework/IOKit";

  [DllImport(IOKitFramework, EntryPoint = "IOPMAssertionCreateWithName")]
  public static extern uint IOPMAssertionCreateWithName(
    nint assertionType,
    uint assertionLevel,
    nint reasonForActivity,
    out uint assertionID);

  [DllImport(IOKitFramework, EntryPoint = "IOPMAssertionRelease")]
  public static extern uint IOPMAssertionRelease(uint assertionID);
}