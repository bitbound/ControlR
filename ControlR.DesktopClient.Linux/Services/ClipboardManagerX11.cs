using System.Runtime.InteropServices;
using System.Text;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

public sealed class ClipboardManagerX11 : IClipboardManager, IDisposable
{
  private static readonly object _lock = new();

  private readonly ILogger<ClipboardManagerX11> _logger;

  private nint _clipboardAtom;
  private nint _clipboardDataAtom;
  private nint _clipboardManagerAtom;
  private string? _currentClipboardText;
  private nint _display;
  private nint _saveTargetsAtom;
  private nint _stringAtom;
  private nint _targetsAtom;
  private nint _utf8StringAtom;
  private nint _window;

  public ClipboardManagerX11(ILogger<ClipboardManagerX11> logger)
  {
    _logger = logger;
    Initialize();
  }

  public void Dispose()
  {
    lock (_lock)
    {
      if (_window != nint.Zero)
      {
        LibX11.XDestroyWindow(_display, _window);
        _window = nint.Zero;
      }

      if (_display != nint.Zero)
      {
        LibX11.XCloseDisplay(_display);
        _display = nint.Zero;
      }
    }
    GC.SuppressFinalize(this);
  }

  public Task<string?> GetText()
  {
    lock (_lock)
    {
      try
      {
        if (_display == nint.Zero)
        {
          Initialize();
        }

        // Only get from the CLIPBOARD selection (Ctrl+C/Ctrl+V)
        var text = GetSelectionText(_clipboardAtom);
        return Task.FromResult(text);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while getting clipboard text.");
        return Task.FromResult<string?>(null);
      }
    }
  }

  public Task SetText(string? text)
  {
    lock (_lock)
    {
      try
      {
        if (_display == nint.Zero)
        {
          Initialize();
        }

        if (string.IsNullOrEmpty(text))
        {
          return Task.CompletedTask;
        }

        SetClipboardText(text);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while setting clipboard text.");
      }
      return Task.CompletedTask;
    }
  }

  private string? GetSelectionText(nint selection)
  {
    var owner = LibX11.XGetSelectionOwner(_display, selection);
    if (owner == nint.Zero)
    {
      return null;
    }

    // Try UTF8_STRING format first, then fallback to STRING format
    return TryGetSelectionWithFormat(selection, _utf8StringAtom) ??
           TryGetSelectionWithFormat(selection, _stringAtom);
  }

  private void HandleEvent(ref LibX11.XEvent xEvent)
  {
    try
    {
      if (xEvent.type == LibX11.SelectionClear)
      {
        // We lost clipboard ownership
        _currentClipboardText = null;
      }
      else if (xEvent.type == LibX11.SelectionRequest)
      {
        HandleSelectionRequest(ref xEvent);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error handling X11 event");
    }
  }

  private void HandleSelectionRequest(ref LibX11.XEvent xEvent)
  {
    // We need to extract the selection request data from the event
    // Since XEvent is a union, we need to be careful about how we access the data
    var eventPtr = Marshal.AllocHGlobal(Marshal.SizeOf<LibX11.XEvent>());
    try
    {
      Marshal.StructureToPtr(xEvent, eventPtr, false);
      var request = Marshal.PtrToStructure<LibX11.XSelectionRequestEvent>(eventPtr);
      
      var response = new LibX11.XSelectionEvent
      {
        type = LibX11.SelectionNotify,
        display = request.display,
        requestor = request.requestor,
        selection = request.selection,
        target = request.target,
        property = nint.Zero,
        time = request.time
      };

      if (request.selection == _clipboardAtom && !string.IsNullOrEmpty(_currentClipboardText))
      {
        if (request.target == _targetsAtom)
        {
          // Client is asking what formats we support
          var supportedTargets = new nint[] { _targetsAtom, _utf8StringAtom, _stringAtom };
          var targetsPtr = Marshal.AllocHGlobal(supportedTargets.Length * nint.Size);
          try
          {
            Marshal.Copy(supportedTargets, 0, targetsPtr, supportedTargets.Length);
            LibX11.XChangeProperty(_display, request.requestor, request.property, 
              LibX11.XInternAtom(_display, "ATOM", false), 32, LibX11.PropModeReplace, targetsPtr, supportedTargets.Length);
            response.property = request.property;
          }
          finally
          {
            Marshal.FreeHGlobal(targetsPtr);
          }
        }
        else if (request.target == _utf8StringAtom || request.target == _stringAtom)
        {
          // Client is requesting text data
          var encoding = request.target == _utf8StringAtom ? Encoding.UTF8 : Encoding.ASCII;
          var textBytes = encoding.GetBytes(_currentClipboardText);
          var textPtr = Marshal.AllocHGlobal(textBytes.Length);
          try
          {
            Marshal.Copy(textBytes, 0, textPtr, textBytes.Length);
            LibX11.XChangeProperty(_display, request.requestor, request.property, request.target, 8, 
              LibX11.PropModeReplace, textPtr, textBytes.Length);
            response.property = request.property;
          }
          finally
          {
            Marshal.FreeHGlobal(textPtr);
          }
        }
      }

      // Send the response event
      var responseEventPtr = Marshal.AllocHGlobal(Marshal.SizeOf<LibX11.XSelectionEvent>());
      try
      {
        Marshal.StructureToPtr(response, responseEventPtr, false);
        var responseEvent = Marshal.PtrToStructure<LibX11.XEvent>(responseEventPtr);
        LibX11.XSendEvent(_display, request.requestor, false, LibX11.NoEventMask, ref responseEvent);
        LibX11.XFlush(_display);
      }
      finally
      {
        Marshal.FreeHGlobal(responseEventPtr);
      }
    }
    finally
    {
      Marshal.FreeHGlobal(eventPtr);
    }
  }

  private void Initialize()
  {
    _display = LibX11.XOpenDisplay(null);
    if (_display == nint.Zero)
    {
      throw new InvalidOperationException("Could not open X11 display");
    }

    var rootWindow = LibX11.XDefaultRootWindow(_display);
    _window = LibX11.XCreateSimpleWindow(_display, rootWindow, 0, 0, 1, 1, 0, 0, 0);

    // Select for events we need to handle - both property changes and selection events
    const long eventMask = LibX11.PropertyChangeMask | (1L << 29) | (1L << 30); // PropertyChange + SelectionClear + SelectionRequest
    LibX11.XSelectInput(_display, _window, eventMask);

    // Intern atoms - clipboard management atoms
    _clipboardAtom = LibX11.XInternAtom(_display, "CLIPBOARD", false);
    _utf8StringAtom = LibX11.XInternAtom(_display, "UTF8_STRING", false);
    _stringAtom = LibX11.XInternAtom(_display, "STRING", false);
    _clipboardDataAtom = LibX11.XInternAtom(_display, "CONTROLR_CLIPBOARD_DATA", false);
    _clipboardManagerAtom = LibX11.XInternAtom(_display, "CLIPBOARD_MANAGER", false);
    _saveTargetsAtom = LibX11.XInternAtom(_display, "SAVE_TARGETS", false);
    _targetsAtom = LibX11.XInternAtom(_display, "TARGETS", false);
  }

  private void ProcessPendingEvents()
  {
    // Process any pending events for a short time to handle immediate selection requests
    var timeout = DateTime.Now.AddMilliseconds(100);
    while (DateTime.Now < timeout && LibX11.XPending(_display) > 0)
    {
      _ = LibX11.XNextEvent(_display, out var xEvent);
      HandleEvent(ref xEvent);
    }
  }

  private string? ReadProperty(nint property)
  {
    var result = LibX11.XGetWindowProperty(_display, _window, property, 0, 1024, true, LibX11.AnyPropertyType,
      out var actualType, out var actualFormat, out var itemCount, out var bytesAfter, out var propData);

    if (result != 0 || propData == nint.Zero || itemCount == 0)
    {
      if (propData != nint.Zero)
      {
        LibX11.XFree(propData);
      }
      return null;
    }

    try
    {
      var data = new byte[itemCount];
      Marshal.Copy(propData, data, 0, (int)itemCount);

      // Try UTF-8 first, then fallback to ASCII
      try
      {
        return Encoding.UTF8.GetString(data);
      }
      catch
      {
        return Encoding.ASCII.GetString(data);
      }
    }
    finally
    {
      LibX11.XFree(propData);
    }
  }

  private void SetClipboardText(string text)
  {
    try
    {
      // Store the text so we can provide it when requested
      _currentClipboardText = text;
      
      // Set property on our window with the text data
      var textBytes = Encoding.UTF8.GetBytes(text);
      var textPtr = Marshal.AllocHGlobal(textBytes.Length);
      try
      {
        Marshal.Copy(textBytes, 0, textPtr, textBytes.Length);
        LibX11.XChangeProperty(_display, _window, _clipboardDataAtom, _utf8StringAtom, 8, LibX11.PropModeReplace, textPtr, textBytes.Length);

        // Take ownership of the clipboard selection
        LibX11.XSetSelectionOwner(_display, _clipboardAtom, _window, LibX11.CurrentTime);

        // Verify we got ownership
        var owner = LibX11.XGetSelectionOwner(_display, _clipboardAtom);
        if (owner != _window)
        {
          _logger.LogWarning("Failed to acquire clipboard ownership");
          return;
        }

        // Process any pending events to handle selection requests
        ProcessPendingEvents();

        // Store atoms in clipboard manager to persist after our process exits
        StoreAtomsInClipboardManager(text);

        LibX11.XFlush(_display);
      }
      finally
      {
        Marshal.FreeHGlobal(textPtr);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to set clipboard text");
    }
  }

  private void StoreAtomsInClipboardManager(string text)
  {
    try
    {
      // Skip storing if text is too large
      if (text.Length * 2 > 64 * 1024)
      {
        return;
      }

      if (_clipboardManagerAtom != nint.Zero && _saveTargetsAtom != nint.Zero)
      {
        var clipboardManager = LibX11.XGetSelectionOwner(_display, _clipboardManagerAtom);
        if (clipboardManager != nint.Zero)
        {
          // Create array of atoms we want to save
          var atoms = new nint[] { _targetsAtom, _utf8StringAtom, _stringAtom };
          var atomPtr = Marshal.AllocHGlobal(atoms.Length * nint.Size);
          try
          {
            Marshal.Copy(atoms, 0, atomPtr, atoms.Length);

            // Create a unique property name for our save request
            var savePropertyAtom = LibX11.XInternAtom(_display, "CONTROLR_SAVE_TARGETS", false);
            LibX11.XChangeProperty(_display, _window, savePropertyAtom, LibX11.XInternAtom(_display, "ATOM", false), 32, LibX11.PropModeReplace, atomPtr, atoms.Length);

            // Request the clipboard manager to save our targets
            LibX11.XConvertSelection(_display, _clipboardManagerAtom, _saveTargetsAtom, savePropertyAtom, _window, LibX11.CurrentTime);
            LibX11.XFlush(_display);
          }
          finally
          {
            Marshal.FreeHGlobal(atomPtr);
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to store atoms in clipboard manager");
    }
  }

  private string? TryGetSelectionWithFormat(nint selection, nint format)
  {
    LibX11.XConvertSelection(_display, selection, format, _clipboardDataAtom, _window, LibX11.CurrentTime);
    LibX11.XFlush(_display);

    var timeout = DateTime.Now.AddSeconds(1);
    while (DateTime.Now < timeout)
    {
      if (LibX11.XPending(_display) > 0)
      {
        _ = LibX11.XNextEvent(_display, out var xEvent);
        
        // Handle any selection requests while we wait for our response
        if (xEvent.type == LibX11.SelectionRequest)
        {
          HandleEvent(ref xEvent);
        }
        else if (xEvent.type == LibX11.SelectionNotify)
        {
          return ReadProperty(_clipboardDataAtom);
        }
        else
        {
          // Handle other events
          HandleEvent(ref xEvent);
        }
      }
      Thread.Sleep(10);
    }

    return null;
  }
}