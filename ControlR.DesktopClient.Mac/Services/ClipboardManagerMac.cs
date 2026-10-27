using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Mac;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

public class ClipboardManagerMac(ILogger<ClipboardManagerMac> logger) : IClipboardManager
{
    private static readonly nint _clearContentsSelector = AppKitInterop.sel_registerName("clearContents");
    private static readonly nint _generalPasteboardSelector = AppKitInterop.sel_registerName("generalPasteboard");
    private static readonly nint _nsPasteboardClass = AppKitInterop.objc_getClass("NSPasteboard");
    private static readonly nint _nsStringPboardType = AppKitInterop.CreateNSString("NSStringPboardType");
    private static readonly nint _setStringForTypeSelector = AppKitInterop.sel_registerName("setString:forType:");
    private static readonly nint _stringForTypeSelector = AppKitInterop.sel_registerName("stringForType:");

    private readonly SemaphoreSlim _clipboardLock = new(1, 1);
    private readonly ILogger<ClipboardManagerMac> _logger = logger;

  public async Task<string?> GetText()
    {
        await _clipboardLock.WaitAsync();
        try
        {
            // Get the general pasteboard
            var pasteboard = AppKitInterop.objc_msgSend(_nsPasteboardClass, _generalPasteboardSelector);
            if (pasteboard == nint.Zero)
                return null;

            // Get string from pasteboard
            var nsString = AppKitInterop.objc_msgSend_IntPtr(pasteboard, _stringForTypeSelector, _nsStringPboardType);
            if (nsString == nint.Zero)
                return null;

            // Convert NSString to C# string
            var result = AppKitInterop.NSStringToString(nsString);
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

    public async Task<Result> SetText(string? text)
    {
      if (text is null)
      {
        return Result.Ok();
      }

      await _clipboardLock.WaitAsync();
      try
      {
        var pasteboard = AppKitInterop.objc_msgSend(_nsPasteboardClass, _generalPasteboardSelector);
        if (pasteboard == nint.Zero)
        {
          return Result.Fail("Could not open the macOS pasteboard.");
        }

        // A change count of -1 means another process still owns the pasteboard, so the write below
        // would land nowhere.  The clipboard being busy is the failure the viewer needs to hear about.
        if (AppKitInterop.objc_msgSend(pasteboard, _clearContentsSelector) == -1)
        {
          return Result.Fail("The macOS pasteboard is in use by another application.");
        }

        var nsString = AppKitInterop.CreateNSString(text);
        if (nsString == nint.Zero)
        {
          return Result.Fail("Could not convert the clipboard text for the macOS pasteboard.");
        }

        var didSet = AppKitInterop.objc_msgSend_Bool_IntPtr_IntPtr(
          pasteboard, _setStringForTypeSelector, nsString, _nsStringPboardType);

        return didSet
          ? Result.Ok()
          : Result.Fail("The macOS pasteboard refused the text.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while setting clipboard text.");
        return Result.Fail(ex);
      }
      finally
      {
        _clipboardLock.Release();
      }
    }
}
