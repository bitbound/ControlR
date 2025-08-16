using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.MacOs;

public static class ApplicationServices
{
  // Framework for Accessibility and Screen Capture permissions
  private const string ApplicationServicesFramework = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

  // Accessibility and Screen Capture Permissions
  [DllImport(ApplicationServicesFramework, EntryPoint = "AXIsProcessTrusted")]
  public static extern bool AXIsProcessTrusted();

  [DllImport(ApplicationServicesFramework, EntryPoint = "AXIsProcessTrustedWithOptions")]
  public static extern bool AXIsProcessTrustedWithOptions(nint options);
}