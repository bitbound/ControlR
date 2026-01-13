using System.Collections.Frozen;
using System.Diagnostics;
using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.NativeInterop.Unix.MacOs;

public interface IMacInterop
{
  Result InvokeKeyEvent(string key, string? code, bool isPressed);
  void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed);
  void InvokeWheelScroll(int x, int y, int scrollY, int scrollX);
  bool IsMacAccessibilityPermissionGranted();
  bool IsMacScreenCapturePermissionGranted();
  void MovePointer(int x, int y, MovePointerType moveType);
  void OpenAccessibilityPreferences();
  void OpenScreenRecordingPreferences();
  void RequestAccessibilityPermission();
  void RequestScreenCapturePermission();
  void ResetKeyboardState();
  void TypeText(string text);
  Result WakeScreen();
}

public class MacInterop(ILogger<MacInterop> logger) : IMacInterop
{
  private const int DoubleClickDistanceThreshold = 5; // pixels

  // Double-click timing and distance thresholds
  private static readonly TimeSpan _doubleClickTimeThreshold = TimeSpan.FromMilliseconds(500);
  private static readonly nint _eventSource = MacInputSimulation.CGEventSourceCreate(MacInputSimulation.kCGHIDEventTap);

  private readonly ILogger<MacInterop> _logger = logger;

  private bool _commandDown;
  private bool _controlDown;
  private int _currentClickCount;
  private int _lastCalculatedClickCount = 1; // Store the last calculated click count for mouse up events
  private int _lastClickButton = -1;

  // Track click timing for double-click detection
  private DateTimeOffset _lastClickTime = DateTimeOffset.MinValue;
  private int _lastClickX = -1;
  private int _lastClickY = -1;

  // Track mouse button states for proper drag events
  private bool _leftButtonDown;
  private bool _middleButtonDown;
  private bool _optionDown;
  private bool _rightButtonDown;

  // Track modifier key states
  private bool _shiftDown;

  public Result InvokeKeyEvent(string key, string? code, bool isPressed)
  {
    if (string.IsNullOrEmpty(key))
    {
      _logger.LogWarning("Key cannot be empty.");
      return Result.Fail("Key cannot be empty.");
    }

    // Handle modifier keys: update state and send modifier event only
    if (key is "Shift" or "Control" or "Alt" or "Meta" or "Command" or "Option")
    {
      switch (key)
      {
        case "Shift": _shiftDown = isPressed; break;
        case "Control": _controlDown = isPressed; break;
        case "Alt":
        case "Command": _commandDown = isPressed; break;
        case "Meta":
        case "Option": _optionDown = isPressed; break;
      }

      // Send modifier key event
      if (!ConvertBrowserKeyArgToVirtualKey(key, code, out var modVirtualKey))
      {
        _logger.LogWarning("Failed to convert modifier key to virtual key: {Key} (code: {Code})", key, code);
        return Result.Fail($"Failed to convert modifier key to virtual key: {key} (code: {code})");
      }
      try
      {
        var modEventRef = MacInputSimulation.CGEventCreateKeyboardEvent(_eventSource, modVirtualKey, isPressed);
        if (modEventRef == nint.Zero)
        {
          _logger.LogWarning("Failed to create modifier event for key: {Key}", key);
          return Result.Fail($"Failed to create modifier event for key: {key}");
        }
        // Set only the modifier flag for this event
        MacInputSimulation.CGEventSetFlags(modEventRef, GetCurrentModifierFlags());
        MacInputSimulation.CGEventPost(MacInputSimulation.kCGHIDEventTap, modEventRef);
        MacInputSimulation.CFRelease(modEventRef);
        return Result.Ok();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error invoking modifier key event for key: {Key}", key);
        return Result.Fail($"Error invoking modifier key event: {ex.Message}");
      }
    }

    // Non-modifier key: set modifier flags if any are down
    if (!ConvertBrowserKeyArgToVirtualKey(key, code, out var virtualKey))
    {
      // TODO: Consider enhanced international keyboard support:
      // 1. Add common international characters to the key map
      // 2. Use system keyboard layout detection to adapt mappings  
      // 3. Research macOS APIs for dynamic keyboard layout detection

      // Fall back to TypeText for unmapped keys (supports Unicode)
      if (key.Length == 1 && isPressed) // Only type on key press, not release
      {
        _logger.LogDebug("Key '{Key}' not in map, falling back to TypeText", key);
        TypeText(key);
        return Result.Ok();
      }

      _logger.LogWarning("Failed to convert key to virtual key: {Key}", key);
      return Result.Fail($"Failed to convert key to virtual key: {key}");
    }

    try
    {
      var eventRef = MacInputSimulation.CGEventCreateKeyboardEvent(_eventSource, virtualKey, isPressed);
      if (eventRef == nint.Zero)
      {
        _logger.LogWarning("Failed to create keyboard event for key: {Key}", key);
        return Result.Fail($"Failed to create keyboard event for key: {key}");
      }

      // Set modifier flags if any are down
      MacInputSimulation.CGEventSetFlags(eventRef, GetCurrentModifierFlags());

      MacInputSimulation.CGEventPost(MacInputSimulation.kCGHIDEventTap, eventRef);
      MacInputSimulation.CFRelease(eventRef);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error invoking key event for key: {Key}", key);
      return Result.Fail($"Error invoking key event: {ex.Message}");
    }
  }

  public void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed)
  {
    try
    {
      var location = new MacInputSimulation.CGPoint(x, y);
      uint mouseType;
      uint mouseButton;
      int clickCount;

      switch (button)
      {
        case 0: // Left button
          _leftButtonDown = isPressed;
          mouseType = isPressed ? MacInputSimulation.kCGEventLeftMouseDown : MacInputSimulation.kCGEventLeftMouseUp;
          mouseButton = MacInputSimulation.kCGMouseButtonLeft;
          break;
        case 1: // Middle button
          _middleButtonDown = isPressed;
          mouseType = isPressed ? MacInputSimulation.kCGEventOtherMouseDown : MacInputSimulation.kCGEventOtherMouseUp;
          mouseButton = MacInputSimulation.kCGMouseButtonCenter;
          break;
        case 2: // Right button
          _rightButtonDown = isPressed;
          mouseType = isPressed ? MacInputSimulation.kCGEventRightMouseDown : MacInputSimulation.kCGEventRightMouseUp;
          mouseButton = MacInputSimulation.kCGMouseButtonRight;
          break;
        default:
          _logger.LogWarning("Unsupported mouse button: {Button}", button);
          return;
      }

      // Calculate click count - for mouse down events, calculate new count; for mouse up, use the last calculated count
      if (isPressed)
      {
        clickCount = CalculateClickCount(x, y, button);
        _lastCalculatedClickCount = clickCount;
      }
      else
      {
        // Use the click count from the corresponding mouse down event
        clickCount = _lastCalculatedClickCount;
      }

      var eventRef = MacInputSimulation.CGEventCreateMouseEvent(_eventSource, mouseType, location, mouseButton);
      if (eventRef == nint.Zero)
      {
        _logger.LogWarning("Failed to create mouse event for button: {Button}", button);
        return;
      }

      // Set the click count for proper double-click recognition
      // Always set the click count, even for single clicks (macOS expects this)
      MacInputSimulation.CGEventSetIntegerValueField(eventRef, MacInputSimulation.kCGMouseEventClickState, clickCount);

      MacInputSimulation.CGEventPost(MacInputSimulation.kCGHIDEventTap, eventRef);
      MacInputSimulation.CFRelease(eventRef);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error invoking mouse button event for button: {Button}", button);
    }
  }

  public void InvokeWheelScroll(int x, int y, int scrollY, int scrollX)
  {
    try
    {
      MovePointer(x, y, MovePointerType.Absolute);

      // Normalize scroll values to direction only, then use a fixed reasonable scroll amount for macOS
      const int macScrollAmount = 3; // Lines to scroll per wheel tick - adjust as needed

      if (scrollY != 0)
      {
        var normalizedScrollY = scrollY > 0 ? macScrollAmount : -macScrollAmount;

        var eventRef = MacInputSimulation.CGEventCreateScrollWheelEvent(
          _eventSource,
          MacInputSimulation.kCGScrollEventUnitLine,
          1, // wheelCount
          normalizedScrollY,
          0);

        if (eventRef != nint.Zero)
        {
          MacInputSimulation.CGEventPost(MacInputSimulation.kCGHIDEventTap, eventRef);
          MacInputSimulation.CFRelease(eventRef);
        }
      }

      if (scrollX != 0)
      {
        var normalizedScrollX = scrollX > 0 ? macScrollAmount : -macScrollAmount;

        var eventRef = MacInputSimulation.CGEventCreateScrollWheelEvent(
          _eventSource,
          MacInputSimulation.kCGScrollEventUnitLine,
          2, // wheelCount
          0,
          normalizedScrollX);

        if (eventRef != nint.Zero)
        {
          MacInputSimulation.CGEventPost(MacInputSimulation.kCGHIDEventTap, eventRef);
          MacInputSimulation.CFRelease(eventRef);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error invoking wheel scroll at ({X}, {Y})", x, y);
    }
  }

  public bool IsMacAccessibilityPermissionGranted()
  {
    try
    {
      return ApplicationServices.AXIsProcessTrusted();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking accessibility permission.");
      return false;
    }
  }

  public bool IsMacScreenCapturePermissionGranted()
  {
    try
    {
      return CoreGraphics.CGPreflightScreenCaptureAccess();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error checking screen capture permission.");
      return false;
    }
  }

  public void MovePointer(int x, int y, MovePointerType moveType)
  {
    try
    {
      var location = new MacInputSimulation.CGPoint(x, y);

      if (moveType == MovePointerType.Absolute)
      {
        // For absolute positioning, check if any mouse button is down to send appropriate drag event
        if (_leftButtonDown || _rightButtonDown || _middleButtonDown)
        {
          uint mouseType;
          uint mouseButton;

          if (_leftButtonDown)
          {
            mouseType = MacInputSimulation.kCGEventLeftMouseDragged;
            mouseButton = MacInputSimulation.kCGMouseButtonLeft;
          }
          else if (_rightButtonDown)
          {
            mouseType = MacInputSimulation.kCGEventRightMouseDragged;
            mouseButton = MacInputSimulation.kCGMouseButtonRight;
          }
          else // _middleButtonDown
          {
            mouseType = MacInputSimulation.kCGEventOtherMouseDragged;
            mouseButton = MacInputSimulation.kCGMouseButtonCenter;
          }

          var dragEventRef = MacInputSimulation.CGEventCreateMouseEvent(_eventSource, mouseType, location, mouseButton);
          if (dragEventRef != nint.Zero)
          {
            MacInputSimulation.CGEventPost(MacInputSimulation.kCGHIDEventTap, dragEventRef);
            MacInputSimulation.CFRelease(dragEventRef);
          }
        }
        else
        {
          // No buttons down, send a mouse moved event to trigger hover states
          var moveEventRef = MacInputSimulation.CGEventCreateMouseEvent(_eventSource, MacInputSimulation.kCGEventMouseMoved, location, 0);
          if (moveEventRef != nint.Zero)
          {
            MacInputSimulation.CGEventPost(MacInputSimulation.kCGHIDEventTap, moveEventRef);
            MacInputSimulation.CFRelease(moveEventRef);
          }
        }
      }
      else
      {
        throw new NotImplementedException("Relative movement is not supported on macOS.");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error moving pointer to ({X}, {Y})", x, y);
    }
  }
  public void OpenAccessibilityPreferences()
  {
    var prefPage = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility";
    Process.Start("open", prefPage);
  }

  public void OpenScreenRecordingPreferences()
  {
    var prefPage = "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture";
    Process.Start("open", prefPage);
  }

  public void RequestAccessibilityPermission()
  {
    var optionsDict = Foundation.CreateAccessibilityPromptDictionary();
    _ = ApplicationServices.AXIsProcessTrustedWithOptions(optionsDict);
    Foundation.CFRelease(optionsDict);
  }

  public void RequestScreenCapturePermission()
  {
    _ = CoreGraphics.CGRequestScreenCaptureAccess();
  }

  public void ResetKeyboardState()
  {
    try
    {
      var keysReleased = 0;

      // Query and release all pressed keys
      // macOS virtual key codes range from 0 to 127
      for (ushort keycode = 0; keycode < 128; keycode++)
      {
        // Query if this virtual key is currently pressed
        if (MacInputSimulation.CGEventSourceKeyState(
          MacInputSimulation.kCGEventSourceStateHIDSystemState, keycode))
        {
          // Create and post key release event
          var keyupEvent = MacInputSimulation.CGEventCreateKeyboardEvent(
            _eventSource, keycode, false);

          if (keyupEvent != nint.Zero)
          {
            MacInputSimulation.CGEventPost(
              MacInputSimulation.kCGHIDEventTap, keyupEvent);
            MacInputSimulation.CFRelease(keyupEvent);
            keysReleased++;
          }
        }
      }

      if (keysReleased > 0)
      {
        _logger.LogDebug("Released {Count} stuck keys during keyboard state reset", keysReleased);
      }
      else
      {
        _logger.LogDebug("No stuck keys found during keyboard state reset");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error resetting keyboard state");
    }
  }

  public void TypeText(string text)
  {
    try
    {
      foreach (var character in text)
      {
        // Create events for each character using Unicode
        var keyDownEvent = MacInputSimulation.CGEventCreateKeyboardEvent(_eventSource, 0, true);
        var keyUpEvent = MacInputSimulation.CGEventCreateKeyboardEvent(_eventSource, 0, false);

        if (keyDownEvent != nint.Zero && keyUpEvent != nint.Zero)
        {
          // Set the Unicode character
          MacInputSimulation.CGEventKeyboardSetUnicodeString(keyDownEvent, 1, [character]);
          MacInputSimulation.CGEventKeyboardSetUnicodeString(keyUpEvent, 1, [character]);

          MacInputSimulation.CGEventPost(MacInputSimulation.kCGHIDEventTap, keyDownEvent);
          MacInputSimulation.CGEventPost(MacInputSimulation.kCGHIDEventTap, keyUpEvent);

          MacInputSimulation.CFRelease(keyDownEvent);
          MacInputSimulation.CFRelease(keyUpEvent);
        }

        // Small delay between characters
        Thread.Sleep(1);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error typing text: {Text}", text);
    }
  }

  public Result WakeScreen()
  {
    nint assertionType = nint.Zero;
    nint reasonString = nint.Zero;
    try
    {
      // Create CFString for assertion type
      assertionType = Foundation.CFStringCreateWithCString(
        nint.Zero,
        IOKit.kIOPMAssertionTypePreventUserIdleDisplaySleep,
        0x08000100); // kCFStringEncodingUTF8

      if (assertionType == nint.Zero)
      {
        return Result.Fail("Failed to create assertion type string");
      }

      // Create CFString for reason
      reasonString = Foundation.CFStringCreateWithCString(
        nint.Zero,
        "ControlR remote control wake",
        0x08000100); // kCFStringEncodingUTF8

      if (reasonString == nint.Zero)
      {
        return Result.Fail("Failed to create reason string");
      }

      // Create power assertion to wake the screen
      var result = IOKit.IOPMAssertionCreateWithName(
        assertionType,
        IOKit.kIOPMAssertionLevelOn,
        reasonString,
        out var assertionId);

      if (result != IOKit.kIOReturnSuccess)
      {
        return Result.Fail($"Failed to create power assertion. IOReturn: {result}");
      }

      // Brief delay to allow the assertion to take effect
      Thread.Sleep(100);

      // Release the assertion immediately after creating it
      // This is enough to wake the screen without keeping it awake permanently
      var releaseResult = IOKit.IOPMAssertionRelease(assertionId);
      if (releaseResult != IOKit.kIOReturnSuccess)
      {
        _logger.LogWarning("Failed to release power assertion {AssertionID}. IOReturn: {Result}",
          assertionId, releaseResult);
      }

      _logger.LogInformation("Screen wake assertion created and released successfully");
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error waking screen");
      return Result.Fail($"Error waking screen: {ex.Message}");
    }
    finally
    {
      // Clean up CFString objects
      if (assertionType != nint.Zero)
        Foundation.CFRelease(assertionType);
      if (reasonString != nint.Zero)
        Foundation.CFRelease(reasonString);
    }
  }

  private static bool ConvertBrowserKeyArgToVirtualKey(string key, string? code, out ushort virtualKey)
  {
    // Code-first approach (physical mode): Try to map browser KeyboardEvent.code to macOS virtual key
    // This provides layout-independent physical key simulation
    // When code is null, we skip this and use logical mode (key-based) instead
    if (!string.IsNullOrWhiteSpace(code))
    {
      virtualKey = code switch
      {
        // Letter keys (physical key position, layout-independent)
        "KeyA" => MacInputSimulation.kVK_ANSI_A,
        "KeyB" => MacInputSimulation.kVK_ANSI_B,
        "KeyC" => MacInputSimulation.kVK_ANSI_C,
        "KeyD" => MacInputSimulation.kVK_ANSI_D,
        "KeyE" => MacInputSimulation.kVK_ANSI_E,
        "KeyF" => MacInputSimulation.kVK_ANSI_F,
        "KeyG" => MacInputSimulation.kVK_ANSI_G,
        "KeyH" => MacInputSimulation.kVK_ANSI_H,
        "KeyI" => MacInputSimulation.kVK_ANSI_I,
        "KeyJ" => MacInputSimulation.kVK_ANSI_J,
        "KeyK" => MacInputSimulation.kVK_ANSI_K,
        "KeyL" => MacInputSimulation.kVK_ANSI_L,
        "KeyM" => MacInputSimulation.kVK_ANSI_M,
        "KeyN" => MacInputSimulation.kVK_ANSI_N,
        "KeyO" => MacInputSimulation.kVK_ANSI_O,
        "KeyP" => MacInputSimulation.kVK_ANSI_P,
        "KeyQ" => MacInputSimulation.kVK_ANSI_Q,
        "KeyR" => MacInputSimulation.kVK_ANSI_R,
        "KeyS" => MacInputSimulation.kVK_ANSI_S,
        "KeyT" => MacInputSimulation.kVK_ANSI_T,
        "KeyU" => MacInputSimulation.kVK_ANSI_U,
        "KeyV" => MacInputSimulation.kVK_ANSI_V,
        "KeyW" => MacInputSimulation.kVK_ANSI_W,
        "KeyX" => MacInputSimulation.kVK_ANSI_X,
        "KeyY" => MacInputSimulation.kVK_ANSI_Y,
        "KeyZ" => MacInputSimulation.kVK_ANSI_Z,

        // Digit keys (main keyboard row)
        "Digit0" => MacInputSimulation.kVK_ANSI_0,
        "Digit1" => MacInputSimulation.kVK_ANSI_1,
        "Digit2" => MacInputSimulation.kVK_ANSI_2,
        "Digit3" => MacInputSimulation.kVK_ANSI_3,
        "Digit4" => MacInputSimulation.kVK_ANSI_4,
        "Digit5" => MacInputSimulation.kVK_ANSI_5,
        "Digit6" => MacInputSimulation.kVK_ANSI_6,
        "Digit7" => MacInputSimulation.kVK_ANSI_7,
        "Digit8" => MacInputSimulation.kVK_ANSI_8,
        "Digit9" => MacInputSimulation.kVK_ANSI_9,

        // Numpad keys
        "Numpad0" => MacInputSimulation.kVK_ANSI_Keypad0,
        "Numpad1" => MacInputSimulation.kVK_ANSI_Keypad1,
        "Numpad2" => MacInputSimulation.kVK_ANSI_Keypad2,
        "Numpad3" => MacInputSimulation.kVK_ANSI_Keypad3,
        "Numpad4" => MacInputSimulation.kVK_ANSI_Keypad4,
        "Numpad5" => MacInputSimulation.kVK_ANSI_Keypad5,
        "Numpad6" => MacInputSimulation.kVK_ANSI_Keypad6,
        "Numpad7" => MacInputSimulation.kVK_ANSI_Keypad7,
        "Numpad8" => MacInputSimulation.kVK_ANSI_Keypad8,
        "Numpad9" => MacInputSimulation.kVK_ANSI_Keypad9,
        "NumpadMultiply" => MacInputSimulation.kVK_ANSI_KeypadMultiply,
        "NumpadAdd" => MacInputSimulation.kVK_ANSI_KeypadPlus,
        "NumpadSubtract" => MacInputSimulation.kVK_ANSI_KeypadMinus,
        "NumpadDecimal" => MacInputSimulation.kVK_ANSI_KeypadDecimal,
        "NumpadDivide" => MacInputSimulation.kVK_ANSI_KeypadDivide,
        "NumpadEnter" => MacInputSimulation.kVK_ANSI_KeypadEnter,
        "NumpadEquals" => MacInputSimulation.kVK_ANSI_KeypadEquals,

        // Function keys
        "F1" => MacInputSimulation.kVK_F1,
        "F2" => MacInputSimulation.kVK_F2,
        "F3" => MacInputSimulation.kVK_F3,
        "F4" => MacInputSimulation.kVK_F4,
        "F5" => MacInputSimulation.kVK_F5,
        "F6" => MacInputSimulation.kVK_F6,
        "F7" => MacInputSimulation.kVK_F7,
        "F8" => MacInputSimulation.kVK_F8,
        "F9" => MacInputSimulation.kVK_F9,
        "F10" => MacInputSimulation.kVK_F10,
        "F11" => MacInputSimulation.kVK_F11,
        "F12" => MacInputSimulation.kVK_F12,
        "F13" => MacInputSimulation.kVK_F13,
        "F14" => MacInputSimulation.kVK_F14,
        "F15" => MacInputSimulation.kVK_F15,
        "F16" => MacInputSimulation.kVK_F16,
        "F17" => MacInputSimulation.kVK_F17,
        "F18" => MacInputSimulation.kVK_F18,
        "F19" => MacInputSimulation.kVK_F19,
        "F20" => MacInputSimulation.kVK_F20,

        // Navigation keys
        "ArrowDown" => MacInputSimulation.kVK_DownArrow,
        "ArrowUp" => MacInputSimulation.kVK_UpArrow,
        "ArrowLeft" => MacInputSimulation.kVK_LeftArrow,
        "ArrowRight" => MacInputSimulation.kVK_RightArrow,
        "Home" => MacInputSimulation.kVK_Home,
        "End" => MacInputSimulation.kVK_End,
        "PageUp" => MacInputSimulation.kVK_PageUp,
        "PageDown" => MacInputSimulation.kVK_PageDown,

        // Editing keys
        "Backspace" => MacInputSimulation.kVK_Delete,
        "Tab" => MacInputSimulation.kVK_Tab,
        "Enter" => MacInputSimulation.kVK_Return,
        "Delete" => MacInputSimulation.kVK_ForwardDelete,
        "Insert" => MacInputSimulation.kVK_Help, // Mac doesn't have Insert, using Help

        // Modifier keys (left/right specific)
        // Mapping: Alt → Command, Meta → Option (matches keyboards with Alt/Cmd and Win/Opt on same keys)
        "ShiftLeft" => MacInputSimulation.kVK_Shift,
        "ShiftRight" => MacInputSimulation.kVK_RightShift,
        "ControlLeft" => MacInputSimulation.kVK_Control,
        "ControlRight" => MacInputSimulation.kVK_RightControl,
        "AltLeft" => MacInputSimulation.kVK_Command,
        "AltRight" => MacInputSimulation.kVK_RightCommand,
        "MetaLeft" => MacInputSimulation.kVK_Option,
        "MetaRight" => MacInputSimulation.kVK_RightOption,

        // Lock keys
        "CapsLock" => MacInputSimulation.kVK_CapsLock,

        // Special keys
        "Escape" => MacInputSimulation.kVK_Escape,
        "Space" => MacInputSimulation.kVK_Space,

        // OEM/Punctuation keys (US layout physical positions)
        "Semicolon" => MacInputSimulation.kVK_ANSI_Semicolon,
        "Equal" => MacInputSimulation.kVK_ANSI_Equal,
        "Comma" => MacInputSimulation.kVK_ANSI_Comma,
        "Minus" => MacInputSimulation.kVK_ANSI_Minus,
        "Period" => MacInputSimulation.kVK_ANSI_Period,
        "Slash" => MacInputSimulation.kVK_ANSI_Slash,
        "Backquote" => MacInputSimulation.kVK_ANSI_Grave,
        "BracketLeft" => MacInputSimulation.kVK_ANSI_LeftBracket,
        "Backslash" => MacInputSimulation.kVK_ANSI_Backslash,
        "BracketRight" => MacInputSimulation.kVK_ANSI_RightBracket,
        "Quote" => MacInputSimulation.kVK_ANSI_Quote,

        _ => ushort.MaxValue
      };

      if (virtualKey != ushort.MaxValue)
      {
        return true;
      }
    }

    // Fallback to key-based mapping for compatibility with older code or edge cases
    // This handles cases where code is not provided (shouldn't happen in modern browsers)
    return GetLegacyKeyMap().TryGetValue(key, out virtualKey);
  }

  private static FrozenDictionary<string, ushort> GetLegacyKeyMap()
  {
    return new Dictionary<string, ushort>
    {
      // Basic keys
      [" "] = MacInputSimulation.kVK_Space,
      ["Enter"] = MacInputSimulation.kVK_Return,
      ["Tab"] = MacInputSimulation.kVK_Tab,
      ["Backspace"] = MacInputSimulation.kVK_Delete,
      ["Delete"] = MacInputSimulation.kVK_ForwardDelete,
      ["Escape"] = MacInputSimulation.kVK_Escape,
      ["Esc"] = MacInputSimulation.kVK_Escape,

      // Arrow keys
      ["ArrowUp"] = MacInputSimulation.kVK_UpArrow,
      ["Up"] = MacInputSimulation.kVK_UpArrow,
      ["ArrowDown"] = MacInputSimulation.kVK_DownArrow,
      ["Down"] = MacInputSimulation.kVK_DownArrow,
      ["ArrowLeft"] = MacInputSimulation.kVK_LeftArrow,
      ["Left"] = MacInputSimulation.kVK_LeftArrow,
      ["ArrowRight"] = MacInputSimulation.kVK_RightArrow,
      ["Right"] = MacInputSimulation.kVK_RightArrow,

      // Navigation keys
      ["Home"] = MacInputSimulation.kVK_Home,
      ["End"] = MacInputSimulation.kVK_End,
      ["PageUp"] = MacInputSimulation.kVK_PageUp,
      ["PageDown"] = MacInputSimulation.kVK_PageDown,

      // Modifier keys
      // Mapping: Alt → Command, Meta → Option (matches keyboards with Alt/Cmd and Win/Opt on same keys)
      ["Shift"] = MacInputSimulation.kVK_Shift,
      ["Control"] = MacInputSimulation.kVK_Control,
      ["Alt"] = MacInputSimulation.kVK_Command,
      ["Meta"] = MacInputSimulation.kVK_Option,
      ["Command"] = MacInputSimulation.kVK_Command,
      ["Option"] = MacInputSimulation.kVK_Option,
      ["CapsLock"] = MacInputSimulation.kVK_CapsLock,

      // Function keys
      ["F1"] = MacInputSimulation.kVK_F1,
      ["F2"] = MacInputSimulation.kVK_F2,
      ["F3"] = MacInputSimulation.kVK_F3,
      ["F4"] = MacInputSimulation.kVK_F4,
      ["F5"] = MacInputSimulation.kVK_F5,
      ["F6"] = MacInputSimulation.kVK_F6,
      ["F7"] = MacInputSimulation.kVK_F7,
      ["F8"] = MacInputSimulation.kVK_F8,
      ["F9"] = MacInputSimulation.kVK_F9,
      ["F10"] = MacInputSimulation.kVK_F10,
      ["F11"] = MacInputSimulation.kVK_F11,
      ["F12"] = MacInputSimulation.kVK_F12,

      // Letters
      ["a"] = MacInputSimulation.kVK_ANSI_A,
      ["b"] = MacInputSimulation.kVK_ANSI_B,
      ["c"] = MacInputSimulation.kVK_ANSI_C,
      ["d"] = MacInputSimulation.kVK_ANSI_D,
      ["e"] = MacInputSimulation.kVK_ANSI_E,
      ["f"] = MacInputSimulation.kVK_ANSI_F,
      ["g"] = MacInputSimulation.kVK_ANSI_G,
      ["h"] = MacInputSimulation.kVK_ANSI_H,
      ["i"] = MacInputSimulation.kVK_ANSI_I,
      ["j"] = MacInputSimulation.kVK_ANSI_J,
      ["k"] = MacInputSimulation.kVK_ANSI_K,
      ["l"] = MacInputSimulation.kVK_ANSI_L,
      ["m"] = MacInputSimulation.kVK_ANSI_M,
      ["n"] = MacInputSimulation.kVK_ANSI_N,
      ["o"] = MacInputSimulation.kVK_ANSI_O,
      ["p"] = MacInputSimulation.kVK_ANSI_P,
      ["q"] = MacInputSimulation.kVK_ANSI_Q,
      ["r"] = MacInputSimulation.kVK_ANSI_R,
      ["s"] = MacInputSimulation.kVK_ANSI_S,
      ["t"] = MacInputSimulation.kVK_ANSI_T,
      ["u"] = MacInputSimulation.kVK_ANSI_U,
      ["v"] = MacInputSimulation.kVK_ANSI_V,
      ["w"] = MacInputSimulation.kVK_ANSI_W,
      ["x"] = MacInputSimulation.kVK_ANSI_X,
      ["y"] = MacInputSimulation.kVK_ANSI_Y,
      // Capital letters
      ["A"] = MacInputSimulation.kVK_ANSI_A,
      ["B"] = MacInputSimulation.kVK_ANSI_B,
      ["C"] = MacInputSimulation.kVK_ANSI_C,
      ["D"] = MacInputSimulation.kVK_ANSI_D,
      ["E"] = MacInputSimulation.kVK_ANSI_E,
      ["F"] = MacInputSimulation.kVK_ANSI_F,
      ["G"] = MacInputSimulation.kVK_ANSI_G,
      ["H"] = MacInputSimulation.kVK_ANSI_H,
      ["I"] = MacInputSimulation.kVK_ANSI_I,
      ["J"] = MacInputSimulation.kVK_ANSI_J,
      ["K"] = MacInputSimulation.kVK_ANSI_K,
      ["L"] = MacInputSimulation.kVK_ANSI_L,
      ["M"] = MacInputSimulation.kVK_ANSI_M,
      ["N"] = MacInputSimulation.kVK_ANSI_N,
      ["O"] = MacInputSimulation.kVK_ANSI_O,
      ["P"] = MacInputSimulation.kVK_ANSI_P,
      ["Q"] = MacInputSimulation.kVK_ANSI_Q,
      ["R"] = MacInputSimulation.kVK_ANSI_R,
      ["S"] = MacInputSimulation.kVK_ANSI_S,
      ["T"] = MacInputSimulation.kVK_ANSI_T,
      ["U"] = MacInputSimulation.kVK_ANSI_U,
      ["V"] = MacInputSimulation.kVK_ANSI_V,
      ["W"] = MacInputSimulation.kVK_ANSI_W,
      ["X"] = MacInputSimulation.kVK_ANSI_X,
      ["Y"] = MacInputSimulation.kVK_ANSI_Y,
      ["Z"] = MacInputSimulation.kVK_ANSI_Z,
      ["z"] = MacInputSimulation.kVK_ANSI_Z,

      // Numbers
      ["0"] = MacInputSimulation.kVK_ANSI_0,
      ["1"] = MacInputSimulation.kVK_ANSI_1,
      ["2"] = MacInputSimulation.kVK_ANSI_2,
      ["3"] = MacInputSimulation.kVK_ANSI_3,
      ["4"] = MacInputSimulation.kVK_ANSI_4,
      ["5"] = MacInputSimulation.kVK_ANSI_5,
      ["6"] = MacInputSimulation.kVK_ANSI_6,
      ["7"] = MacInputSimulation.kVK_ANSI_7,
      ["8"] = MacInputSimulation.kVK_ANSI_8,
      ["9"] = MacInputSimulation.kVK_ANSI_9,

      // Special characters
      ["-"] = MacInputSimulation.kVK_ANSI_Minus,
      ["="] = MacInputSimulation.kVK_ANSI_Equal,
      ["["] = MacInputSimulation.kVK_ANSI_LeftBracket,
      ["]"] = MacInputSimulation.kVK_ANSI_RightBracket,
      ["\\"] = MacInputSimulation.kVK_ANSI_Backslash,
      [";"] = MacInputSimulation.kVK_ANSI_Semicolon,
      ["'"] = MacInputSimulation.kVK_ANSI_Quote,
      ["`"] = MacInputSimulation.kVK_ANSI_Grave,
      [","] = MacInputSimulation.kVK_ANSI_Comma,
      ["."] = MacInputSimulation.kVK_ANSI_Period,
      ["/"] = MacInputSimulation.kVK_ANSI_Slash,

      // Shifted special characters (symbols)
      ["!"] = MacInputSimulation.kVK_ANSI_1,        // Shift+1
      ["@"] = MacInputSimulation.kVK_ANSI_2,        // Shift+2
      ["#"] = MacInputSimulation.kVK_ANSI_3,        // Shift+3
      ["$"] = MacInputSimulation.kVK_ANSI_4,        // Shift+4
      ["%"] = MacInputSimulation.kVK_ANSI_5,        // Shift+5
      ["^"] = MacInputSimulation.kVK_ANSI_6,        // Shift+6
      ["&"] = MacInputSimulation.kVK_ANSI_7,        // Shift+7
      ["*"] = MacInputSimulation.kVK_ANSI_8,        // Shift+8
      ["("] = MacInputSimulation.kVK_ANSI_9,        // Shift+9
      [")"] = MacInputSimulation.kVK_ANSI_0,        // Shift+0
      ["_"] = MacInputSimulation.kVK_ANSI_Minus,    // Shift+-
      ["+"] = MacInputSimulation.kVK_ANSI_Equal,    // Shift+=
      ["{"] = MacInputSimulation.kVK_ANSI_LeftBracket,  // Shift+[
      ["}"] = MacInputSimulation.kVK_ANSI_RightBracket, // Shift+]
      ["|"] = MacInputSimulation.kVK_ANSI_Backslash,    // Shift+\
      [":"] = MacInputSimulation.kVK_ANSI_Semicolon,    // Shift+;
      ["\""] = MacInputSimulation.kVK_ANSI_Quote,       // Shift+'
      ["~"] = MacInputSimulation.kVK_ANSI_Grave,        // Shift+`
      ["<"] = MacInputSimulation.kVK_ANSI_Comma,        // Shift+,
      [">"] = MacInputSimulation.kVK_ANSI_Period,       // Shift+.
      ["?"] = MacInputSimulation.kVK_ANSI_Slash,        // Shift+/

      // Numpad
      ["Numpad0"] = MacInputSimulation.kVK_ANSI_Keypad0,
      ["Numpad1"] = MacInputSimulation.kVK_ANSI_Keypad1,
      ["Numpad2"] = MacInputSimulation.kVK_ANSI_Keypad2,
      ["Numpad3"] = MacInputSimulation.kVK_ANSI_Keypad3,
      ["Numpad4"] = MacInputSimulation.kVK_ANSI_Keypad4,
      ["Numpad5"] = MacInputSimulation.kVK_ANSI_Keypad5,
      ["Numpad6"] = MacInputSimulation.kVK_ANSI_Keypad6,
      ["Numpad7"] = MacInputSimulation.kVK_ANSI_Keypad7,
      ["Numpad8"] = MacInputSimulation.kVK_ANSI_Keypad8,
      ["Numpad9"] = MacInputSimulation.kVK_ANSI_Keypad9,
      ["NumpadDecimal"] = MacInputSimulation.kVK_ANSI_KeypadDecimal,
      ["NumpadMultiply"] = MacInputSimulation.kVK_ANSI_KeypadMultiply,
      ["NumpadAdd"] = MacInputSimulation.kVK_ANSI_KeypadPlus,
      ["NumpadSubtract"] = MacInputSimulation.kVK_ANSI_KeypadMinus,
      ["NumpadDivide"] = MacInputSimulation.kVK_ANSI_KeypadDivide,
      ["NumpadEnter"] = MacInputSimulation.kVK_ANSI_KeypadEnter,
      ["NumpadEquals"] = MacInputSimulation.kVK_ANSI_KeypadEquals,
      ["Clear"] = MacInputSimulation.kVK_ANSI_KeypadClear,
    }.ToFrozenDictionary();
  }

  private int CalculateClickCount(int x, int y, int button)
  {
    var now = DateTimeOffset.UtcNow;
    var timeSinceLastClick = now - _lastClickTime;
    var distanceFromLastClick = Math.Sqrt(Math.Pow(x - _lastClickX, 2) + Math.Pow(y - _lastClickY, 2));

    // Check if this is a continuation of the click sequence
    if (_lastClickButton == button &&
        timeSinceLastClick <= _doubleClickTimeThreshold &&
        distanceFromLastClick <= DoubleClickDistanceThreshold)
    {
      _currentClickCount++;
    }
    else
    {
      // Start a new click sequence
      _currentClickCount = 1;
    }

    // Update tracking variables
    _lastClickTime = now;
    _lastClickX = x;
    _lastClickY = y;
    _lastClickButton = button;

    return _currentClickCount;
  }

  // Returns the current modifier flags for CGEventSetFlags
  private ulong GetCurrentModifierFlags()
  {
    ulong flags = 0;
    if (_shiftDown)
      flags |= MacInputSimulation.kCGEventFlagMaskShift;
    if (_controlDown)
      flags |= MacInputSimulation.kCGEventFlagMaskControl;
    if (_optionDown)
      flags |= MacInputSimulation.kCGEventFlagMaskAlternate;
    if (_commandDown)
      flags |= MacInputSimulation.kCGEventFlagMaskCommand;
    return flags;
  }

}
