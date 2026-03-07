using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Mac;

public static class AppKit
{
    private const string AppKitFramework = "/System/Library/Frameworks/AppKit.framework/AppKit";

    // Helper methods for NSString conversion
    public static nint CreateNSString(string str)
    {
        var nsStringClass = objc_getClass("NSString");
        var stringWithUTF8StringSelector = sel_registerName("stringWithUTF8String:");
        var cString = Marshal.StringToHGlobalAnsi(str);
        try
        {
            return objc_msgSend_IntPtr(nsStringClass, stringWithUTF8StringSelector, cString);
        }
        finally
        {
            Marshal.FreeHGlobal(cString);
        }
    }

    // NSCursor helper methods
    public static nint GetCurrentCursor()
    {
        var nsCursorClass = objc_getClass("NSCursor");
        var currentSystemCursorSelector = sel_registerName("currentSystemCursor");
        return objc_msgSend(nsCursorClass, currentSystemCursorSelector);
    }

    public static (double x, double y) GetCursorHotspot(nint cursor)
    {
        var hotSpotSelector = sel_registerName("hotSpot");
        // hotSpot returns an NSPoint (two doubles) by value.
        var point = objc_msgSend_CGPoint(cursor, hotSpotSelector);
        return (point.X, point.Y);
    }

    public static nint GetCursorImage(nint cursor)
    {
        var imageSelector = sel_registerName("image");
        return objc_msgSend(cursor, imageSelector);
    }

    public static (double x, double y) GetMouseLocation()
    {
        var nsEventClass = objc_getClass("NSEvent");
        var mouseLocationSelector = sel_registerName("mouseLocation");
        // mouseLocation returns an NSPoint (two doubles) by value.
        var point = objc_msgSend_CGPoint(nsEventClass, mouseLocationSelector);
        return (point.X, point.Y);
    }

    public static nint GetNSImageCGImage(nint nsImage)
    {
        // Get CGImageForProposedRect:context:hints:
        var cgImageSelector = sel_registerName("CGImageForProposedRect:context:hints:");
        return objc_msgSend_IntPtr_IntPtr_IntPtr(nsImage, cgImageSelector, nint.Zero, nint.Zero, nint.Zero);
    }

    public static string? NSStringToString(nint nsString)
    {
        if (nsString == nint.Zero)
            return null;

        var utf8StringSelector = sel_registerName("UTF8String");
        var cStringPtr = objc_msgSend(nsString, utf8StringSelector);
        return cStringPtr != nint.Zero ? Marshal.PtrToStringAnsi(cStringPtr) : null;
    }

    // NSPasteboard methods
    [DllImport(AppKitFramework, EntryPoint = "objc_getClass")]
    public static extern nint objc_getClass(string className);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern CoreGraphics.CGPoint objc_msgSend_CGPoint(nint receiver, nint selector);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern nint objc_msgSend_IntPtr(nint receiver, nint selector, nint arg1);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern nint objc_msgSend_IntPtr_IntPtr(nint receiver, nint selector, nint arg1, nint arg2);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern nint objc_msgSend_IntPtr_IntPtr_IntPtr(nint receiver, nint selector, nint arg1, nint arg2, nint arg3);

    [DllImport(AppKitFramework, EntryPoint = "sel_registerName")]
    public static extern nint sel_registerName(string selectorName);
}
