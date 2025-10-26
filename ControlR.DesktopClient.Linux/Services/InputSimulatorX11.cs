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

  public void InvokeKeyEvent(string key, bool isPressed)
  {
    var display = LibX11.XOpenDisplay("");
    if (display == nint.Zero)
    {
      _logger.LogError("Failed to open X display for key event");
      return;
    }

    try
    {
      var keySym = ConvertBrowserKeyArgToX11Key(key);
      var keySymPtr = LibX11.XStringToKeysym(keySym);

      if (keySymPtr == nint.Zero)
      {
        _logger.LogWarning("Failed to convert key symbol: {Key} -> {KeySym}", key, keySym);
        return;
      }

      var keycode = LibX11.XKeysymToKeycode(display, keySymPtr);
      if (keycode == 0)
      {
        _logger.LogWarning("Failed to get keycode for key symbol: {KeySym}", keySym);
        return;
      }

      LibXtst.XTestFakeKeyEvent(display, keycode, isPressed, 0);
      LibX11.XSync(display, false);
    }
    finally
    {
      LibX11.XCloseDisplay(display);
    }
  }

  public void InvokeMouseButtonEvent(int x, int y, DisplayInfo? display, int button, bool isPressed)
  {
    var xDisplay = LibX11.XOpenDisplay("");
    if (xDisplay == nint.Zero)
    {
      _logger.LogError("Failed to open X display for mouse button event");
      return;
    }

    try
    {
      // Convert coordinates if display is specified
      var (screenNumber, adjustedX, adjustedY) = GetDisplayCoordinates(xDisplay, x, y, display);

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
  }

  public void MovePointer(int x, int y, DisplayInfo? display, MovePointerType moveType)
  {
    var xDisplay = LibX11.XOpenDisplay("");
    if (xDisplay == nint.Zero)
    {
      _logger.LogError("Failed to open X display for mouse move");
      return;
    }

    try
    {
      if (moveType == MovePointerType.Relative)
      {
        LibXtst.XTestFakeRelativeMotionEvent(xDisplay, x, y, 0);
      }
      else
      {
        var (screenNumber, adjustedX, adjustedY) = GetDisplayCoordinates(xDisplay, x, y, display);
        LibXtst.XTestFakeMotionEvent(xDisplay, screenNumber, adjustedX, adjustedY, 0);
      }

      LibX11.XSync(xDisplay, false);
    }
    finally
    {
      LibX11.XCloseDisplay(xDisplay);
    }
  }

  public void ResetKeyboardState()
  {
    // For X11, we don't need to explicitly reset keyboard state
    // The X server manages key state automatically
    _logger.LogDebug("ResetKeyboardState called - no action needed for X11");
  }

  public void ScrollWheel(int x, int y, DisplayInfo? display, int scrollY, int scrollX)
  {
    var xDisplay = LibX11.XOpenDisplay("");
    if (xDisplay == nint.Zero)
    {
      _logger.LogError("Failed to open X display for scroll wheel");
      return;
    }

    try
    {
      var (screenNumber, adjustedX, adjustedY) = GetDisplayCoordinates(xDisplay, x, y, display);

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
  }

  public Task SetBlockInput(bool isBlocked)
  {
    throw new NotImplementedException();
  }

  public void TypeText(string text)
  {
    var display = LibX11.XOpenDisplay("");
    if (display == nint.Zero)
    {
      _logger.LogError("Failed to open X display for typing text");
      return;
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
  }

  private static string ConvertBrowserKeyArgToX11Key(string key)
  {
    var keySym = key switch
    {
      "ArrowDown" => "Down",
      "ArrowUp" => "Up",
      "ArrowLeft" => "Left",
      "ArrowRight" => "Right",
      "Enter" => "Return",
      "Esc" => "Escape",
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
    return keySym;
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