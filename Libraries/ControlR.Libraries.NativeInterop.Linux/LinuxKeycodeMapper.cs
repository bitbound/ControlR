namespace ControlR.Libraries.NativeInterop.Linux;

/// <summary>
/// Maps browser key codes to Linux evdev keycodes for Wayland RemoteDesktop portal.
/// Based on Linux input-event-codes.h and browser KeyboardEvent.code values.
/// </summary>
public static class LinuxKeycodeMapper
{
  /// <summary>
  /// Maps a browser KeyboardEvent.code to a Linux evdev keycode.
  /// Returns -1 if the mapping is not found.
  /// </summary>
  public static int BrowserCodeToLinuxKeycode(string? code, string? key)
  {
    // Code-first approach: if we have a KeyboardEvent.code, prefer it
    if (!string.IsNullOrWhiteSpace(code))
    {
      var result = code switch
      {
        // Letters
        "KeyA" => 30,
        "KeyB" => 48,
        "KeyC" => 46,
        "KeyD" => 32,
        "KeyE" => 18,
        "KeyF" => 33,
        "KeyG" => 34,
        "KeyH" => 35,
        "KeyI" => 23,
        "KeyJ" => 36,
        "KeyK" => 37,
        "KeyL" => 38,
        "KeyM" => 50,
        "KeyN" => 49,
        "KeyO" => 24,
        "KeyP" => 25,
        "KeyQ" => 16,
        "KeyR" => 19,
        "KeyS" => 31,
        "KeyT" => 20,
        "KeyU" => 22,
        "KeyV" => 47,
        "KeyW" => 17,
        "KeyX" => 45,
        "KeyY" => 21,
        "KeyZ" => 44,

        // Numbers
        "Digit0" => 11,
        "Digit1" => 2,
        "Digit2" => 3,
        "Digit3" => 4,
        "Digit4" => 5,
        "Digit5" => 6,
        "Digit6" => 7,
        "Digit7" => 8,
        "Digit8" => 9,
        "Digit9" => 10,

        // Function keys
        "F1" => 59,
        "F2" => 60,
        "F3" => 61,
        "F4" => 62,
        "F5" => 63,
        "F6" => 64,
        "F7" => 65,
        "F8" => 66,
        "F9" => 67,
        "F10" => 68,
        "F11" => 87,
        "F12" => 88,

        // Modifiers
        "ShiftLeft" => 42,
        "ShiftRight" => 54,
        "ControlLeft" => 29,
        "ControlRight" => 97,
        "AltLeft" => 56,
        "AltRight" => 100,
        "MetaLeft" => 125,
        "MetaRight" => 126,

        // Special keys
        "Enter" => 28,
        "Escape" => 1,
        "Backspace" => 14,
        "Tab" => 15,
        "Space" => 57,
        "CapsLock" => 58,

        // Punctuation
        "Minus" => 12,
        "Equal" => 13,
        "BracketLeft" => 26,
        "BracketRight" => 27,
        "Backslash" => 43,
        "Semicolon" => 39,
        "Quote" => 40,
        "Backquote" => 41,
        "Comma" => 51,
        "Period" => 52,
        "Slash" => 53,

        // Navigation
        "ArrowUp" => 103,
        "ArrowDown" => 108,
        "ArrowLeft" => 105,
        "ArrowRight" => 106,
        "PageUp" => 104,
        "PageDown" => 109,
        "Home" => 102,
        "End" => 107,
        "Insert" => 110,
        "Delete" => 111,

        // Numpad
        "Numpad0" => 82,
        "Numpad1" => 79,
        "Numpad2" => 80,
        "Numpad3" => 81,
        "Numpad4" => 75,
        "Numpad5" => 76,
        "Numpad6" => 77,
        "Numpad7" => 71,
        "Numpad8" => 72,
        "Numpad9" => 73,
        "NumpadMultiply" => 55,
        "NumpadAdd" => 78,
        "NumpadSubtract" => 74,
        "NumpadDecimal" => 83,
        "NumpadDivide" => 98,
        "NumpadEnter" => 96,
        "NumLock" => 69,

        // Other
        "ScrollLock" => 70,
        "Pause" => 119,
        "PrintScreen" => 99,

        _ => -1
      };

      if (result != -1)
      {
        return result;
      }
    }

    // Fallback: use key name when code is missing or unrecognized (case-insensitive)
    if (!string.IsNullOrWhiteSpace(key))
    {
      switch (key.Trim().ToLowerInvariant())
      {
        case "enter":
          return 28;
        case "backspace":
          return 14;
        case "tab":
          return 15;
        case "escape":
        case "esc":
          return 1;
        case "space":
          return 57;
        case "arrowup":
        case "up":
          return 103;
        case "arrowdown":
        case "down":
          return 108;
        case "arrowleft":
        case "left":
          return 105;
        case "arrowright":
        case "right":
          return 106;
        default:
          return -1;
      }
    }

    return -1;
  }

  public static string? BrowserKeyToKeysymName(string? key)
  {
    if (string.IsNullOrWhiteSpace(key))
    {
      return null;
    }

    if (key.Length == 1)
    {
      return CharacterToKeysymName(key[0]);
    }

    if (key.Length > 1 &&
        key[0] == 'F' &&
        int.TryParse(key.AsSpan(1), out var functionNumber) &&
        functionNumber is >= 1 and <= 35)
    {
      return key;
    }

    return key switch
    {
      "ArrowDown" => "Down",
      "ArrowUp" => "Up",
      "ArrowLeft" => "Left",
      "ArrowRight" => "Right",
      "Enter" => "Return",
      "Esc" => "Escape",
      "Escape" => "Escape",
      "Alt" => "Alt_L",
      "Control" => "Control_L",
      "Shift" => "Shift_L",
      "Meta" => "Super_L",
      "Backspace" => "BackSpace",
      "Tab" => "Tab",
      "CapsLock" => "Caps_Lock",
      "Delete" => "Delete",
      "Insert" => "Insert",
      "Home" => "Home",
      "End" => "End",
      "PageUp" => "Page_Up",
      "PageDown" => "Page_Down",
      "NumLock" => "Num_Lock",
      "ScrollLock" => "Scroll_Lock",
      "Pause" => "Pause",
      "ContextMenu" => "Menu",
      "PrintScreen" => "Print",
      _ => null,
    };
  }

  public static string? CharacterToKeysymName(char character)
  {
    return character switch
    {
      >= 'a' and <= 'z' => character.ToString(),
      >= 'A' and <= 'Z' => character.ToString(),
      >= '0' and <= '9' => character.ToString(),
      ' ' => "space",
      '!' => "exclam",
      '"' => "quotedbl",
      '#' => "numbersign",
      '$' => "dollar",
      '%' => "percent",
      '&' => "ampersand",
      '\'' => "apostrophe",
      '(' => "parenleft",
      ')' => "parenright",
      '*' => "asterisk",
      '+' => "plus",
      ',' => "comma",
      '-' => "minus",
      '.' => "period",
      '/' => "slash",
      ':' => "colon",
      ';' => "semicolon",
      '<' => "less",
      '=' => "equal",
      '>' => "greater",
      '?' => "question",
      '@' => "at",
      '[' => "bracketleft",
      '\\' => "backslash",
      ']' => "bracketright",
      '^' => "asciicircum",
      '_' => "underscore",
      '`' => "grave",
      '{' => "braceleft",
      '|' => "bar",
      '}' => "braceright",
      '~' => "asciitilde",
      '\t' => "Tab",
      '\n' or '\r' => "Return",
      _ => null,
    };
  }

  /// <summary>
  /// Maps mouse button number to Linux evdev button code.
  /// </summary>
  public static int MouseButtonToLinuxCode(int button)
  {
    return button switch
    {
      0 => 0x110, // BTN_LEFT
      1 => 0x112, // BTN_MIDDLE
      2 => 0x111, // BTN_RIGHT
      3 => 0x113, // BTN_SIDE
      4 => 0x114, // BTN_EXTRA
      _ => 0x110  // Default to left
    };
  }

  public static bool TryMapCharacterToLinuxKeycode(char ch, out int keycode, out bool needsShift)
  {
    (keycode, needsShift) = ch switch
    {
      >= 'a' and <= 'z' => (BrowserCodeToLinuxKeycode($"Key{char.ToUpperInvariant(ch)}", ch.ToString()), false),
      >= 'A' and <= 'Z' => (BrowserCodeToLinuxKeycode($"Key{ch}", ch.ToString()), true),
      >= '0' and <= '9' => (BrowserCodeToLinuxKeycode($"Digit{ch}", ch.ToString()), false),
      ' ' => (57, false),
      '!' => (2, true),
      '@' => (3, true),
      '#' => (4, true),
      '$' => (5, true),
      '%' => (6, true),
      '^' => (7, true),
      '&' => (8, true),
      '*' => (9, true),
      '(' => (10, true),
      ')' => (11, true),
      '-' => (12, false),
      '_' => (12, true),
      '=' => (13, false),
      '+' => (13, true),
      '[' => (26, false),
      '{' => (26, true),
      ']' => (27, false),
      '}' => (27, true),
      '\\' => (43, false),
      '|' => (43, true),
      ';' => (39, false),
      ':' => (39, true),
      '\'' => (40, false),
      '"' => (40, true),
      ',' => (51, false),
      '<' => (51, true),
      '.' => (52, false),
      '>' => (52, true),
      '/' => (53, false),
      '?' => (53, true),
      '`' => (41, false),
      '~' => (41, true),
      '\n' or '\r' => (28, false),
      '\t' => (15, false),
      _ => (-1, false)
    };

    return keycode >= 0;
  }
}
