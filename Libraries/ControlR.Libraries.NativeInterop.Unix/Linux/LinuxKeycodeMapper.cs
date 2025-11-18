namespace ControlR.Libraries.NativeInterop.Unix.Linux;

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
  public static int BrowserCodeToLinuxKeycode(string? browserCode)
  {
    if (string.IsNullOrEmpty(browserCode))
    {
      return -1;
    }

    return browserCode switch
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
}
