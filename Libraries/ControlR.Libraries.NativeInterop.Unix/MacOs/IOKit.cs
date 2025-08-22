#pragma warning disable IDE1006 // Naming Styles
using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.MacOs;

public static class IOKit
{
  private const string IOKitFramework = "/System/Library/Frameworks/IOKit.framework/IOKit";

  // IOPMAssertion types
  public const string kIOPMAssertionTypePreventUserIdleDisplaySleep = "PreventUserIdleDisplaySleep";
  public const string kIOPMAssertionTypePreventUserIdleSystemSleep = "PreventUserIdleSystemSleep";
  public const string kIOPMAssertionTypePreventSystemSleep = "PreventSystemSleep";

  // IOReturn codes
  public const uint kIOReturnSuccess = 0;

  [DllImport(IOKitFramework, EntryPoint = "IOPMAssertionCreateWithName")]
  public static extern uint IOPMAssertionCreateWithName(
    nint assertionType,
    uint assertionLevel,
    nint reasonForActivity,
    out uint assertionID);

  [DllImport(IOKitFramework, EntryPoint = "IOPMAssertionRelease")]
  public static extern uint IOPMAssertionRelease(uint assertionID);

  // IOPMAssertionLevel values
  public const uint kIOPMAssertionLevelOff = 0;
  public const uint kIOPMAssertionLevelOn = 255;
}