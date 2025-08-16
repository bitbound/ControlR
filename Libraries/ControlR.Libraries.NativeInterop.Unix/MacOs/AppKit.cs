using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.MacOs;

public static class AppKit
{
    private const string AppKitFramework = "/System/Library/Frameworks/AppKit.framework/AppKit";
    
    // NSPasteboard methods
    [DllImport(AppKitFramework, EntryPoint = "objc_getClass")]
    public static extern nint objc_getClass(string className);

    [DllImport(AppKitFramework, EntryPoint = "sel_registerName")]
    public static extern nint sel_registerName(string selectorName);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern nint objc_msgSend_IntPtr(nint receiver, nint selector, nint arg1);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern bool objc_msgSend_bool(nint receiver, nint selector);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern bool objc_msgSend_bool_IntPtr(nint receiver, nint selector, nint arg1);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern nint objc_msgSend_IntPtr_IntPtr(nint receiver, nint selector, nint arg1, nint arg2);

    // NSString methods
    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern nint objc_msgSend_IntPtr_nuint(nint receiver, nint selector, nint arg1, nuint arg2);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern nuint objc_msgSend_nuint(nint receiver, nint selector);

    // NSUserNotification methods for toast notifications
    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern void objc_msgSend_void(nint receiver, nint selector);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern void objc_msgSend_void_IntPtr(nint receiver, nint selector, nint arg1);

    [DllImport(AppKitFramework, EntryPoint = "objc_msgSend")]
    public static extern nint objc_msgSend_alloc_init(nint receiver, nint selector);

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

    // Helper methods for creating NSUserNotification
    public static nint CreateNSUserNotification()
    {
        var nsUserNotificationClass = objc_getClass("NSUserNotification");
        var allocSelector = sel_registerName("alloc");
        var initSelector = sel_registerName("init");
        var notification = objc_msgSend(nsUserNotificationClass, allocSelector);
        return objc_msgSend(notification, initSelector);
    }

    public static void SetNotificationTitle(nint notification, string title)
    {
        var setTitleSelector = sel_registerName("setTitle:");
        var titleNSString = CreateNSString(title);
        objc_msgSend_void_IntPtr(notification, setTitleSelector, titleNSString);
    }

    public static void SetNotificationInformativeText(nint notification, string message)
    {
        var setInformativeTextSelector = sel_registerName("setInformativeText:");
        var messageNSString = CreateNSString(message);
        objc_msgSend_void_IntPtr(notification, setInformativeTextSelector, messageNSString);
    }

    public static void SetNotificationSoundName(nint notification, string soundName)
    {
        var setSoundNameSelector = sel_registerName("setSoundName:");
        var soundNSString = CreateNSString(soundName);
        objc_msgSend_void_IntPtr(notification, setSoundNameSelector, soundNSString);
    }

    public static void DeliverNotification(nint notification)
    {
        var nsUserNotificationCenterClass = objc_getClass("NSUserNotificationCenter");
        var defaultUserNotificationCenterSelector = sel_registerName("defaultUserNotificationCenter");
        var deliverNotificationSelector = sel_registerName("deliverNotification:");
        
        var notificationCenter = objc_msgSend(nsUserNotificationCenterClass, defaultUserNotificationCenterSelector);
        objc_msgSend_void_IntPtr(notificationCenter, deliverNotificationSelector, notification);
    }

    private static nint GetNSStringConstant(string name)
    {
        var nsStringClass = objc_getClass("NSString");
        var stringWithUTF8StringSelector = sel_registerName("stringWithUTF8String:");
        return objc_msgSend_IntPtr(nsStringClass, stringWithUTF8StringSelector, Marshal.StringToHGlobalAnsi(name));
    }

    // Constants for NSPasteboard
    public static readonly nint NSStringPboardType = GetNSStringConstant("NSStringPboardType");
}
