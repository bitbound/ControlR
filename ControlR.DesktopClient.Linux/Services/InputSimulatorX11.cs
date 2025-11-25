using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

public class InputSimulatorX11 : IInputSimulator
{
  private readonly ILogger<InputSimulatorX11> _logger;

  public InputSimulatorX11(ILogger<InputSimulatorX11> logger)
  {
    _logger = logger;
  }

  public Task InvokeKeyEvent(string key, string? code, bool isPressed)
  {
    // Hybrid approach: route printable characters to Unicode injection, commands to virtual key simulation
    // When code is null/empty, it indicates a printable character that should be typed (not simulated as key)
    var isPrintableCharacter = string.IsNullOrWhiteSpace(code) && key.Length == 1;

    if (isPrintableCharacter)
    {
      // For printable characters, use Unicode injection on key down only
      // Key up events are ignored since TypeText handles both down and up internally
      if (isPressed)
      {
        return TypeText(key);
      }
      return Task.CompletedTask;
    }

    // For commands, shortcuts, and non-printable keys, use virtual key simulation
    var display = LibX11.XOpenDisplay("");
    if (display == nint.Zero)
    {
      _logger.LogError("Failed to open X display for key event");
      return Task.CompletedTask;
    }

    try
    {
      var keySym = ConvertBrowserKeyArgToX11Key(key, code);
      var keySymPtr = LibX11.XStringToKeysym(keySym);

      if (keySymPtr == nint.Zero)
      {
        _logger.LogWarning("Failed to convert key symbol: {Key} ({Code}) -> {KeySym}", key, code, keySym);
        return Task.CompletedTask;
      }

      var keycode = LibX11.XKeysymToKeycode(display, keySymPtr);
      if (keycode == 0)
      {
        _logger.LogWarning("Failed to get keycode for key symbol: {KeySym}", keySym);
        return Task.CompletedTask;
      }

      LibXtst.XTestFakeKeyEvent(display, keycode, isPressed, 0);
      LibX11.XSync(display, false);
    }
    finally
    {
      LibX11.XCloseDisplay(display);
    }

    return Task.CompletedTask;
  }

  public Task InvokeMouseButtonEvent(PointerCoordinates coordinates, int button, bool isPressed)
  {
    var xDisplay = LibX11.XOpenDisplay("");
    if (xDisplay == nint.Zero)
    {
      _logger.LogError("Failed to open X display for mouse button event");
      return Task.CompletedTask;
    }

    try
    {
      // Convert coordinates if display is specified
      var (screenNumber, adjustedX, adjustedY) = GetDisplayCoordinates(xDisplay, coordinates.AbsolutePoint.X, coordinates.AbsolutePoint.Y, coordinates.Display);

      // Move cursor to position first
      LibXtst.XTestFakeMotionEvent(xDisplay, screenNumber, adjustedX, adjustedY, 0);

      // X11 mouse button mapping: 1=left, 2=middle, 3=right, 4=scroll up, 5=scroll down
      uint xButton = button switch
      {
        0 => 1, // Left button
        1 => 2, // Middle button
        2 => 3, // Right button
        _ => (uint)button + 1
      };

      LibXtst.XTestFakeButtonEvent(xDisplay, xButton, isPressed, 0);
      LibX11.XSync(xDisplay, false);
    }
    finally
    {
      LibX11.XCloseDisplay(xDisplay);
    }

    return Task.CompletedTask;
  }

  public Task MovePointer(PointerCoordinates coordinates, MovePointerType moveType)
  {
    var xDisplay = LibX11.XOpenDisplay("");
    if (xDisplay == nint.Zero)
    {
      _logger.LogError("Failed to open X display for mouse move");
      return Task.CompletedTask;
    }

    try
    {
      if (moveType == MovePointerType.Relative)
      {
        LibXtst.XTestFakeRelativeMotionEvent(xDisplay, coordinates.AbsolutePoint.X, coordinates.AbsolutePoint.Y, 0);
      }
      else
      {
        var (screenNumber, adjustedX, adjustedY) = GetDisplayCoordinates(xDisplay, coordinates.AbsolutePoint.X, coordinates.AbsolutePoint.Y, coordinates.Display);
        LibXtst.XTestFakeMotionEvent(xDisplay, screenNumber, adjustedX, adjustedY, 0);
      }

      LibX11.XSync(xDisplay, false);
    }
    finally
    {
      LibX11.XCloseDisplay(xDisplay);
    }

    return Task.CompletedTask;
  }

  public unsafe Task ResetKeyboardState()
  {
    var display = LibX11.XOpenDisplay("");
    if (display == nint.Zero)
    {
      _logger.LogWarning("Failed to open X display for keyboard state reset");
      return Task.CompletedTask;
    }

    try
    {
      // Query current keyboard state (32 bytes = 256 bits for 256 keycodes)
      var keymap = stackalloc byte[32];
      var result = LibX11.XQueryKeymap(display, keymap);

      if (result == 0)
      {
        _logger.LogWarning("XQueryKeymap failed during keyboard state reset");
        return Task.CompletedTask;
      }

      var keysReleased = 0;

      // Release all pressed keys
      // Keycodes start at 8 on most X11 servers (0-7 are reserved)
      for (int keycode = 8; keycode < 256; keycode++)
      {
        int byteIndex = keycode / 8;
        int bitIndex = keycode % 8;

        // Check if this key is currently pressed
        if ((keymap[byteIndex] & (1 << bitIndex)) != 0)
        {
          // Send key release event
          LibXtst.XTestFakeKeyEvent(display, (byte)keycode, false, 0);
          keysReleased++;
        }
      }

      LibX11.XSync(display, false);

      if (keysReleased > 0)
      {
        _logger.LogDebug("Released {Count} stuck keys during keyboard state reset", keysReleased);
      }
      else
      {
        _logger.LogDebug("No stuck keys found during keyboard state reset");
      }
    }
    finally
    {
      LibX11.XCloseDisplay(display);
    }

    return Task.CompletedTask;
  }

  public Task ScrollWheel(PointerCoordinates coordinates, int scrollY, int scrollX)
  {
    var xDisplay = LibX11.XOpenDisplay("");
    if (xDisplay == nint.Zero)
    {
      _logger.LogError("Failed to open X display for scroll wheel");
      return Task.CompletedTask;
    }

    try
    {
      var (screenNumber, adjustedX, adjustedY) = GetDisplayCoordinates(xDisplay, coordinates.AbsolutePoint.X, coordinates.AbsolutePoint.Y, coordinates.Display);

      // Move cursor to position first
      LibXtst.XTestFakeMotionEvent(xDisplay, screenNumber, adjustedX, adjustedY, 0);

      // Handle vertical scrolling
      if (scrollY != 0)
      {
        uint scrollButton = scrollY > 0 ? 4u : 5u; // 4=scroll up, 5=scroll down
        // Use fixed scroll steps for consistent behavior
        const int scrollSteps = 3;

        for (int i = 0; i < scrollSteps; i++)
        {
          LibXtst.XTestFakeButtonEvent(xDisplay, scrollButton, true, 0);
          LibXtst.XTestFakeButtonEvent(xDisplay, scrollButton, false, 0);
        }
      }

      // Handle horizontal scrolling (buttons 6 and 7)
      if (scrollX != 0)
      {
        uint scrollButton = scrollX > 0 ? 7u : 6u; // 6=scroll left, 7=scroll right
        // Use fixed scroll steps for consistent behavior
        const int scrollSteps = 3;

        for (int i = 0; i < scrollSteps; i++)
        {
          LibXtst.XTestFakeButtonEvent(xDisplay, scrollButton, true, 0);
          LibXtst.XTestFakeButtonEvent(xDisplay, scrollButton, false, 0);
        }
      }

      LibX11.XSync(xDisplay, false);
    }
    finally
    {
      LibX11.XCloseDisplay(xDisplay);
    }

    return Task.CompletedTask;
  }

  public Task SetBlockInput(bool isBlocked)
  {
    throw new NotImplementedException();
  }

  public Task TypeText(string text)
  {
    var display = LibX11.XOpenDisplay("");
    if (display == nint.Zero)
    {
      _logger.LogError("Failed to open X display for typing text");
      return Task.CompletedTask;
    }

    try
    {
      // Get Shift key code for modifier
      var shiftKeySymPtr = LibX11.XStringToKeysym("Shift_L");
      var shiftKeycode = LibX11.XKeysymToKeycode(display, shiftKeySymPtr);

      foreach (var character in text)
      {
        var (keySym, needsShift) = ConvertCharacterToX11KeySymWithModifier(character);
        var keySymPtr = LibX11.XStringToKeysym(keySym);

        if (keySymPtr == nint.Zero)
        {
          _logger.LogWarning("Failed to convert character to key symbol: {Character} -> {KeySym}", character, keySym);
          continue;
        }

        var keycode = LibX11.XKeysymToKeycode(display, keySymPtr);
        if (keycode == 0)
        {
          _logger.LogWarning("Failed to get keycode for character: {Character}", character);
          continue;
        }

        // If character needs Shift modifier, press Shift first
        if (needsShift && shiftKeycode != 0)
        {
          LibXtst.XTestFakeKeyEvent(display, shiftKeycode, true, 0);
        }

        // Send key press and release
        LibXtst.XTestFakeKeyEvent(display, keycode, true, 0);
        LibXtst.XTestFakeKeyEvent(display, keycode, false, 0);

        // Release Shift if it was pressed
        if (needsShift && shiftKeycode != 0)
        {
          LibXtst.XTestFakeKeyEvent(display, shiftKeycode, false, 0);
        }
      }

      LibX11.XSync(display, false);
    }
    finally
    {
      LibX11.XCloseDisplay(display);
    }

    return Task.CompletedTask;
  }

  private static string ConvertBrowserKeyArgToX11Key(string key, string? code)
  {
    // Code-first approach (physical mode): Try to map browser KeyboardEvent.code to X11 keysym
    // This provides layout-independent physical key simulation
    // When code is null, we skip this and use logical mode (key-based) instead
    if (!string.IsNullOrWhiteSpace(code))
    {
      var keySym = code switch
      {
        // Letter keys (physical key position, layout-independent)
        "KeyA" => "a",
        "KeyB" => "b",
        "KeyC" => "c",
        "KeyD" => "d",
        "KeyE" => "e",
        "KeyF" => "f",
        "KeyG" => "g",
        "KeyH" => "h",
        "KeyI" => "i",
        "KeyJ" => "j",
        "KeyK" => "k",
        "KeyL" => "l",
        "KeyM" => "m",
        "KeyN" => "n",
        "KeyO" => "o",
        "KeyP" => "p",
        "KeyQ" => "q",
        "KeyR" => "r",
        "KeyS" => "s",
        "KeyT" => "t",
        "KeyU" => "u",
        "KeyV" => "v",
        "KeyW" => "w",
        "KeyX" => "x",
        "KeyY" => "y",
        "KeyZ" => "z",

        // Digit keys (main keyboard row)
        "Digit0" => "0",
        "Digit1" => "1",
        "Digit2" => "2",
        "Digit3" => "3",
        "Digit4" => "4",
        "Digit5" => "5",
        "Digit6" => "6",
        "Digit7" => "7",
        "Digit8" => "8",
        "Digit9" => "9",

        // Numpad keys
        "Numpad0" => "KP_0",
        "Numpad1" => "KP_1",
        "Numpad2" => "KP_2",
        "Numpad3" => "KP_3",
        "Numpad4" => "KP_4",
        "Numpad5" => "KP_5",
        "Numpad6" => "KP_6",
        "Numpad7" => "KP_7",
        "Numpad8" => "KP_8",
        "Numpad9" => "KP_9",
        "NumpadMultiply" => "KP_Multiply",
        "NumpadAdd" => "KP_Add",
        "NumpadSubtract" => "KP_Subtract",
        "NumpadDecimal" => "KP_Decimal",
        "NumpadDivide" => "KP_Divide",
        "NumpadEnter" => "KP_Enter",

        // Function keys
        "F1" => "F1",
        "F2" => "F2",
        "F3" => "F3",
        "F4" => "F4",
        "F5" => "F5",
        "F6" => "F6",
        "F7" => "F7",
        "F8" => "F8",
        "F9" => "F9",
        "F10" => "F10",
        "F11" => "F11",
        "F12" => "F12",
        "F13" => "F13",
        "F14" => "F14",
        "F15" => "F15",
        "F16" => "F16",
        "F17" => "F17",
        "F18" => "F18",
        "F19" => "F19",
        "F20" => "F20",
        "F21" => "F21",
        "F22" => "F22",
        "F23" => "F23",
        "F24" => "F24",

        // Navigation keys
        "ArrowDown" => "Down",
        "ArrowUp" => "Up",
        "ArrowLeft" => "Left",
        "ArrowRight" => "Right",
        "Home" => "Home",
        "End" => "End",
        "PageUp" => "Page_Up",
        "PageDown" => "Page_Down",

        // Editing keys
        "Backspace" => "BackSpace",
        "Tab" => "Tab",
        "Enter" => "Return",
        "Delete" => "Delete",
        "Insert" => "Insert",

        // Modifier keys
        "ShiftLeft" => "Shift_L",
        "ShiftRight" => "Shift_R",
        "ControlLeft" => "Control_L",
        "ControlRight" => "Control_R",
        "AltLeft" => "Alt_L",
        "AltRight" => "Alt_R",
        "MetaLeft" => "Super_L",
        "MetaRight" => "Super_R",

        // Lock keys
        "CapsLock" => "Caps_Lock",
        "NumLock" => "Num_Lock",
        "ScrollLock" => "Scroll_Lock",

        // Special keys
        "Escape" => "Escape",
        "Space" => "space",
        "Pause" => "Pause",
        "ContextMenu" => "Menu",
        "PrintScreen" => "Print",

        // OEM/Punctuation keys (US layout physical positions)
        "Semicolon" => "semicolon",
        "Equal" => "equal",
        "Comma" => "comma",
        "Minus" => "minus",
        "Period" => "period",
        "Slash" => "slash",
        "Backquote" => "grave",
        "BracketLeft" => "bracketleft",
        "Backslash" => "backslash",
        "BracketRight" => "bracketright",
        "Quote" => "apostrophe",
        "IntlBackslash" => "less", // <> key (non-US keyboards)

        // Media keys
        "AudioVolumeUp" => "XF86AudioRaiseVolume",
        "AudioVolumeDown" => "XF86AudioLowerVolume",
        "AudioVolumeMute" => "XF86AudioMute",
        "MediaTrackNext" => "XF86AudioNext",
        "MediaTrackPrevious" => "XF86AudioPrev",
        "MediaStop" => "XF86AudioStop",
        "MediaPlayPause" => "XF86AudioPlay",

        // Browser keys
        "BrowserBack" => "XF86Back",
        "BrowserForward" => "XF86Forward",
        "BrowserRefresh" => "XF86Reload",
        "BrowserStop" => "XF86Stop",
        "BrowserSearch" => "XF86Search",
        "BrowserFavorites" => "XF86Favorites",
        "BrowserHome" => "XF86HomePage",

        // Japanese/Korean IME keys
        "Convert" => "Henkan",
        "NonConvert" => "Muhenkan",
        "KanaMode" => "Hiragana_Katakana",
        "KanjiMode" => "Kanji",
        "Lang1" => "Hangul",
        "Lang2" => "Hangul_Hanja",

        _ => null
      };

      if (keySym is not null)
      {
        return keySym;
      }
    }

    // Fallback to key-based mapping for compatibility with older code or edge cases
    // This handles cases where code is not provided (shouldn't happen in modern browsers)
    var fallbackKeySym = key switch
    {
      "ArrowDown" => "Down",
      "ArrowUp" => "Up",
      "ArrowLeft" => "Left",
      "ArrowRight" => "Right",
      "Enter" => "Return",
      "Esc" or "Escape" => "Escape",
      "Alt" => "Alt_L",
      "Control" => "Control_L",
      "Shift" => "Shift_L",
      "PAUSE" => "Pause",
      "BREAK" => "Break",
      "Backspace" => "BackSpace",
      "Tab" => "Tab",
      "CapsLock" => "Caps_Lock",
      "Delete" => "Delete",
      "PageUp" => "Page_Up",
      "PageDown" => "Page_Down",
      "NumLock" => "Num_Lock",
      "ScrollLock" => "Scroll_Lock",
      "ContextMenu" => "Menu",
      " " => "space",
      "!" => "exclam",
      "\"" => "quotedbl",
      "#" => "numbersign",
      "$" => "dollar",
      "%" => "percent",
      "&" => "ampersand",
      "'" => "apostrophe",
      "(" => "parenleft",
      ")" => "parenright",
      "*" => "asterisk",
      "+" => "plus",
      "," => "comma",
      "-" => "minus",
      "." => "period",
      "/" => "slash",
      ":" => "colon",
      ";" => "semicolon",
      "<" => "less",
      "=" => "equal",
      ">" => "greater",
      "?" => "question",
      "@" => "at",
      "[" => "bracketleft",
      "\\" => "backslash",
      "]" => "bracketright",
      "_" => "underscore",
      "`" => "grave",
      "{" => "braceleft",
      "|" => "bar",
      "}" => "braceright",
      "~" => "asciitilde",
      _ => key,
    };
    return fallbackKeySym;
  }

  private static (string keySym, bool needsShift) ConvertCharacterToX11KeySymWithModifier(char character)
  {
    // Direct character to X11 keysym mapping with Shift modifier info
    return character switch
    {
      // Lowercase letters - no shift needed
      >= 'a' and <= 'z' => (character.ToString(), false),

      // Uppercase letters - need shift + lowercase equivalent
      >= 'A' and <= 'Z' => (char.ToLower(character).ToString(), true),

      // Numbers - no shift needed
      >= '0' and <= '9' => (character.ToString(), false),

      // Symbols that need Shift (top row symbols)
      '!' => ("1", true),
      '@' => ("2", true),
      '#' => ("3", true),
      '$' => ("4", true),
      '%' => ("5", true),
      '^' => ("6", true),
      '&' => ("7", true),
      '*' => ("8", true),
      '(' => ("9", true),
      ')' => ("0", true),

      // Other symbols that need Shift
      '_' => ("minus", true),
      '+' => ("equal", true),
      '{' => ("bracketleft", true),
      '}' => ("bracketright", true),
      '|' => ("backslash", true),
      ':' => ("semicolon", true),
      '"' => ("apostrophe", true),
      '<' => ("comma", true),
      '>' => ("period", true),
      '?' => ("slash", true),
      '~' => ("grave", true),

      // Symbols that don't need Shift
      ' ' => ("space", false),
      '-' => ("minus", false),
      '=' => ("equal", false),
      '[' => ("bracketleft", false),
      ']' => ("bracketright", false),
      '\\' => ("backslash", false),
      ';' => ("semicolon", false),
      '\'' => ("apostrophe", false),
      ',' => ("comma", false),
      '.' => ("period", false),
      '/' => ("slash", false),
      '`' => ("grave", false),

      // Common whitespace characters
      '\t' => ("Tab", false),
      '\n' => ("Return", false),
      '\r' => ("Return", false),

      // Default case for any other character
      _ => (character.ToString(), false)
    };
  }

  private static (int screenNumber, int adjustedX, int adjustedY) GetDisplayCoordinates(nint xDisplay, int x, int y, DisplayInfo? display)
  {
    // Default to screen 0 if no display info provided
    if (display == null)
    {
      return (0, x, y);
    }

    // For multi-monitor setups, we need to adjust coordinates based on display position
    // Use the MonitorArea rectangle to get the position offset
    var adjustedX = x + display.MonitorArea.Left;
    var adjustedY = y + display.MonitorArea.Top;

    // Get the appropriate screen number (typically 0 for most setups)
    var screenNumber = LibX11.XDefaultScreen(xDisplay);

    return (screenNumber, adjustedX, adjustedY);
  }

}