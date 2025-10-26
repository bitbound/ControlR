using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.MacOs;

public static class CoreGraphics
{
  private const string CoreGraphicsFramework = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

  [DllImport(CoreGraphicsFramework, EntryPoint = "CFDataGetBytePtr")]
  public static extern nint CFDataGetBytePtr(nint theData);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CFDataGetLength")]
  public static extern nint CFDataGetLength(nint theData);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CFRelease")]
  public static extern void CFRelease(nint cf);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGCursorIsDrawnInFramebuffer")]
  public static extern bool CGCursorIsDrawnInFramebuffer();

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGCursorIsVisible")]
  public static extern bool CGCursorIsVisible();

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGDataProviderCopyData")]
  public static extern nint CGDataProviderCopyData(nint provider);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGDisplayBounds")]
  public static extern CGRect CGDisplayBounds(uint display);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGDisplayCreateImage")]
  public static extern nint CGDisplayCreateImage(uint display);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGDisplayCreateImageForRect")]
  public static extern nint CGDisplayCreateImageForRect(uint display, CGRect rect);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGDisplayIsMain")]
  public static extern bool CGDisplayIsMain(uint display);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGDisplayMoveCursorToPoint")]
  public static extern int CGDisplayMoveCursorToPoint(uint display, CGPoint point);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGDisplayPixelsHigh")]
  public static extern nint CGDisplayPixelsHigh(uint display);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGDisplayPixelsWide")]
  public static extern nint CGDisplayPixelsWide(uint display);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventCreateMouseEvent")]
  public static extern nint CGEventCreateMouseEvent(nint source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventPost")]
  public static extern void CGEventPost(uint tap, nint @event);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventSourceCreate")]
  public static extern nint CGEventSourceCreate(uint stateID);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGGetDisplaysWithRect")]
  public static extern int CGGetDisplaysWithRect(CGRect rect, uint maxDisplays, uint[] displays, out uint matchingDisplayCount);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGGetOnlineDisplayList")]
  public static extern int CGGetOnlineDisplayList(uint maxDisplays, uint[] displays, out uint displayCount);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGImageGetBitsPerComponent")]
  public static extern nint CGImageGetBitsPerComponent(nint image);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGImageGetBitsPerPixel")]
  public static extern nint CGImageGetBitsPerPixel(nint image);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGImageGetBytesPerRow")]
  public static extern nint CGImageGetBytesPerRow(nint image);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGImageGetDataProvider")]
  public static extern nint CGImageGetDataProvider(nint image);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGImageGetHeight")]
  public static extern nint CGImageGetHeight(nint image);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGImageGetWidth")]
  public static extern nint CGImageGetWidth(nint image);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGMainDisplayID")]
  public static extern uint CGMainDisplayID();

  // Remote Desktop permission APIs
  [DllImport(CoreGraphicsFramework, EntryPoint = "CGPreflightPostEventAccess")]
  public static extern bool CGPreflightPostEventAccess();
  
  [DllImport(CoreGraphicsFramework, EntryPoint = "CGPreflightScreenCaptureAccess")]
  public static extern bool CGPreflightScreenCaptureAccess();

  // Alternative method that might be available in some macOS versions
  [DllImport(CoreGraphicsFramework, EntryPoint = "CGRequestListenEventAccess")]
  public static extern bool CGRequestListenEventAccess();

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGRequestPostEventAccess")]
  public static extern bool CGRequestPostEventAccess();

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGRequestScreenCaptureAccess")]
  public static extern bool CGRequestScreenCaptureAccess();

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGWarpMouseCursorPosition")]
  public static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);

  [StructLayout(LayoutKind.Sequential)]
  public struct CGPoint
  {
    public double X;
    public double Y;

    public CGPoint(double x, double y)
    {
      X = x;
      Y = y;
    }
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct CGRect
  {
    public CGPoint Origin;
    public CGSize Size;

    public CGRect(double x, double y, double width, double height)
    {
      Origin = new CGPoint(x, y);
      Size = new CGSize(width, height);
    }

    public double X => Origin.X;
    public double Y => Origin.Y;
    public double Width => Size.Width;
    public double Height => Size.Height;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct CGSize
  {
    public double Width;
    public double Height;

    public CGSize(double width, double height)
    {
      Width = width;
      Height = height;
    }
  }
}
