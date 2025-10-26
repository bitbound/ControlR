using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

public class ClipboardManagerMac(ILogger<ClipboardManagerMac> logger) : IClipboardManager
{
    private static readonly nint _clearContentsSelector = AppKit.sel_registerName("clearContents");
    private static readonly nint _generalPasteboardSelector = AppKit.sel_registerName("generalPasteboard");
    private static readonly nint _nsPasteboardClass = AppKit.objc_getClass("NSPasteboard");
    private static readonly nint _nsStringPboardType = AppKit.CreateNSString("NSStringPboardType");
    private static readonly nint _setStringForTypeSelector = AppKit.sel_registerName("setString:forType:");
    private static readonly nint _stringForTypeSelector = AppKit.sel_registerName("stringForType:");

    private readonly SemaphoreSlim _clipboardLock = new(1, 1);
    private readonly ILogger<ClipboardManagerMac> _logger = logger;

  public async Task<string?> GetText()
    {
        await _clipboardLock.WaitAsync();
        try
        {
            // Get the general pasteboard
            var pasteboard = AppKit.objc_msgSend(_nsPasteboardClass, _generalPasteboardSelector);
            if (pasteboard == nint.Zero)
                return null;

            // Get string from pasteboard
            var nsString = AppKit.objc_msgSend_IntPtr(pasteboard, _stringForTypeSelector, _nsStringPboardType);
            if (nsString == nint.Zero)
                return null;

            // Convert NSString to C# string
            var result = AppKit.NSStringToString(nsString);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting clipboard text.");
            return null;
        }
        finally
        {
            _clipboardLock.Release();
        }
    }

    public async Task SetText(string? text)
    {
        if (text == null)
        {
            return;
        }

        await _clipboardLock.WaitAsync();
        try
        {
            // Get the general pasteboard
            var pasteboard = AppKit.objc_msgSend(_nsPasteboardClass, _generalPasteboardSelector);
            if (pasteboard == nint.Zero)
                return;

            // Clear existing contents
            AppKit.objc_msgSend(pasteboard, _clearContentsSelector);

            // Create NSString from C# string
            var nsString = AppKit.CreateNSString(text);
            if (nsString == nint.Zero)
                return;

            // Set string in pasteboard
            AppKit.objc_msgSend_IntPtr_IntPtr(pasteboard, _setStringForTypeSelector, nsString, _nsStringPboardType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while setting clipboard text.");
        }
        finally
        {
            _clipboardLock.Release();
        }
    }
}
