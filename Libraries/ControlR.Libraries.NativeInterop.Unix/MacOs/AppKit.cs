using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.MacOs;

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
    public static extern nint objc_msgSend_IntPtr(nint receiver, nint selector, nint arg1);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern nint objc_msgSend_IntPtr_IntPtr(nint receiver, nint selector, nint arg1, nint arg2);

    [DllImport(AppKitFramework, EntryPoint = "sel_registerName")]
    public static extern nint sel_registerName(string selectorName);
}
