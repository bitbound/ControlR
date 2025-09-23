using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace ControlR.DesktopClient.Linux.Services;

public class ClipboardManagerGtk : IClipboardManager, IDisposable
{
  private readonly ILogger<ClipboardManagerGtk> _logger;
  private static readonly object _lock = new();
  private static bool _gtkInitialized = false;
  private bool _disposed = false;

  public ClipboardManagerGtk(ILogger<ClipboardManagerGtk> logger)
  {
    _logger = logger;
    EnsureGtkInitialized();
  }

  public Task<string?> GetText()
  {
    return Task.Run(() =>
    {
      lock (_lock)
      {
        try
        {
          if (!EnsureGtkInitialized())
          {
            return null;
          }

          var clipboard = LibGtk.gtk_clipboard_get(LibGtk.GDK_SELECTION_CLIPBOARD);
          if (clipboard == nint.Zero)
          {
            _logger.LogError("Failed to get GTK clipboard");
            return null;
          }

          var textPtr = LibGtk.gtk_clipboard_wait_for_text(clipboard);
          if (textPtr == nint.Zero)
          {
            return null;
          }

          try
          {
            var text = Marshal.PtrToStringUTF8(textPtr);
            return text;
          }
          finally
          {
            // Free the memory allocated by GTK
            LibGtk.g_free(textPtr);
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error getting clipboard text via GTK");
          return null;
        }
      }
    });
  }

  public Task SetText(string? text)
  {
    return Task.Run(() =>
    {
      lock (_lock)
      {
        try
        {
          if (string.IsNullOrEmpty(text))
          {
            return;
          }

          if (!EnsureGtkInitialized())
          {
            return;
          }

          var clipboard = LibGtk.gtk_clipboard_get(LibGtk.GDK_SELECTION_CLIPBOARD);
          if (clipboard == nint.Zero)
          {
            _logger.LogError("Failed to get GTK clipboard");
            return;
          }

          // Set the text (-1 means null-terminated string)
          LibGtk.gtk_clipboard_set_text(clipboard, text, -1);
          
          // Store the clipboard to make it persistent
          LibGtk.gtk_clipboard_store(clipboard);
          
          _logger.LogDebug("Set clipboard text via GTK: {Length} characters", text.Length);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error setting clipboard text via GTK");
        }
      }
    });
  }

  private bool EnsureGtkInitialized()
  {
    if (_gtkInitialized)
      return true;

    try
    {
      int argc = 0;
      var result = LibGtk.gtk_init_check(ref argc, nint.Zero);
      _gtkInitialized = result;
      
      if (!result)
      {
        _logger.LogError("Failed to initialize GTK");
      }
      else
      {
        _logger.LogDebug("GTK initialized successfully");
      }
      
      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Exception during GTK initialization");
      return false;
    }
  }

  public void Dispose()
  {
    if (!_disposed)
    {
      _disposed = true;
      GC.SuppressFinalize(this);
    }
  }
}