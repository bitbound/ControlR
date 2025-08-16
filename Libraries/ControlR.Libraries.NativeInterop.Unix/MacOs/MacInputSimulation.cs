#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable IDE1006 // Naming Styles
using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.MacOs;

public static class MacInputSimulation
{
  private const string CoreGraphicsFramework = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

  // Event types
  public const uint kCGEventLeftMouseDown = 1;
  public const uint kCGEventLeftMouseUp = 2;
  public const uint kCGEventRightMouseDown = 3;
  public const uint kCGEventRightMouseUp = 4;
  public const uint kCGEventMouseMoved = 5;
  public const uint kCGEventLeftMouseDragged = 6;
  public const uint kCGEventRightMouseDragged = 7;
  public const uint kCGEventKeyDown = 10;
  public const uint kCGEventKeyUp = 11;
  public const uint kCGEventScrollWheel = 22;
  public const uint kCGEventOtherMouseDown = 25;
  public const uint kCGEventOtherMouseUp = 26;
  public const uint kCGEventOtherMouseDragged = 27;

  // Mouse buttons
  public const uint kCGMouseButtonLeft = 0;
  public const uint kCGMouseButtonRight = 1;
  public const uint kCGMouseButtonCenter = 2;

  // Key codes (Virtual key codes)
  public const ushort kVK_ANSI_A = 0x00;
  public const ushort kVK_ANSI_S = 0x01;
  public const ushort kVK_ANSI_D = 0x02;
  public const ushort kVK_ANSI_F = 0x03;
  public const ushort kVK_ANSI_H = 0x04;
  public const ushort kVK_ANSI_G = 0x05;
  public const ushort kVK_ANSI_Z = 0x06;
  public const ushort kVK_ANSI_X = 0x07;
  public const ushort kVK_ANSI_C = 0x08;
  public const ushort kVK_ANSI_V = 0x09;
  public const ushort kVK_ANSI_B = 0x0B;
  public const ushort kVK_ANSI_Q = 0x0C;
  public const ushort kVK_ANSI_W = 0x0D;
  public const ushort kVK_ANSI_E = 0x0E;
  public const ushort kVK_ANSI_R = 0x0F;
  public const ushort kVK_ANSI_Y = 0x10;
  public const ushort kVK_ANSI_T = 0x11;
  public const ushort kVK_ANSI_1 = 0x12;
  public const ushort kVK_ANSI_2 = 0x13;
  public const ushort kVK_ANSI_3 = 0x14;
  public const ushort kVK_ANSI_4 = 0x15;
  public const ushort kVK_ANSI_6 = 0x16;
  public const ushort kVK_ANSI_5 = 0x17;
  public const ushort kVK_ANSI_Equal = 0x18;
  public const ushort kVK_ANSI_9 = 0x19;
  public const ushort kVK_ANSI_7 = 0x1A;
  public const ushort kVK_ANSI_Minus = 0x1B;
  public const ushort kVK_ANSI_8 = 0x1C;
  public const ushort kVK_ANSI_0 = 0x1D;
  public const ushort kVK_ANSI_RightBracket = 0x1E;
  public const ushort kVK_ANSI_O = 0x1F;
  public const ushort kVK_ANSI_U = 0x20;
  public const ushort kVK_ANSI_LeftBracket = 0x21;
  public const ushort kVK_ANSI_I = 0x22;
  public const ushort kVK_ANSI_P = 0x23;
  public const ushort kVK_ANSI_L = 0x25;
  public const ushort kVK_ANSI_J = 0x26;
  public const ushort kVK_ANSI_Quote = 0x27;
  public const ushort kVK_ANSI_K = 0x28;
  public const ushort kVK_ANSI_Semicolon = 0x29;
  public const ushort kVK_ANSI_Backslash = 0x2A;
  public const ushort kVK_ANSI_Comma = 0x2B;
  public const ushort kVK_ANSI_Slash = 0x2C;
  public const ushort kVK_ANSI_N = 0x2D;
  public const ushort kVK_ANSI_M = 0x2E;
  public const ushort kVK_ANSI_Period = 0x2F;
  public const ushort kVK_ANSI_Grave = 0x32;
  public const ushort kVK_ANSI_KeypadDecimal = 0x41;
  public const ushort kVK_ANSI_KeypadMultiply = 0x43;
  public const ushort kVK_ANSI_KeypadPlus = 0x45;
  public const ushort kVK_ANSI_KeypadClear = 0x47;
  public const ushort kVK_ANSI_KeypadDivide = 0x4B;
  public const ushort kVK_ANSI_KeypadEnter = 0x4C;
  public const ushort kVK_ANSI_KeypadMinus = 0x4E;
  public const ushort kVK_ANSI_KeypadEquals = 0x51;
  public const ushort kVK_ANSI_Keypad0 = 0x52;
  public const ushort kVK_ANSI_Keypad1 = 0x53;
  public const ushort kVK_ANSI_Keypad2 = 0x54;
  public const ushort kVK_ANSI_Keypad3 = 0x55;
  public const ushort kVK_ANSI_Keypad4 = 0x56;
  public const ushort kVK_ANSI_Keypad5 = 0x57;
  public const ushort kVK_ANSI_Keypad6 = 0x58;
  public const ushort kVK_ANSI_Keypad7 = 0x59;
  public const ushort kVK_ANSI_Keypad8 = 0x5B;
  public const ushort kVK_ANSI_Keypad9 = 0x5C;

  // Function keys
  public const ushort kVK_Return = 0x24;
  public const ushort kVK_Tab = 0x30;
  public const ushort kVK_Space = 0x31;
  public const ushort kVK_Delete = 0x33;
  public const ushort kVK_Escape = 0x35;
  public const ushort kVK_Command = 0x37;
  public const ushort kVK_Shift = 0x38;
  public const ushort kVK_CapsLock = 0x39;
  public const ushort kVK_Option = 0x3A;
  public const ushort kVK_Control = 0x3B;
  public const ushort kVK_RightShift = 0x3C;
  public const ushort kVK_RightOption = 0x3D;
  public const ushort kVK_RightControl = 0x3E;
  public const ushort kVK_Function = 0x3F;
  public const ushort kVK_F17 = 0x40;
  public const ushort kVK_VolumeUp = 0x48;
  public const ushort kVK_VolumeDown = 0x49;
  public const ushort kVK_Mute = 0x4A;
  public const ushort kVK_F18 = 0x4F;
  public const ushort kVK_F19 = 0x50;
  public const ushort kVK_F20 = 0x5A;
  public const ushort kVK_F5 = 0x60;
  public const ushort kVK_F6 = 0x61;
  public const ushort kVK_F7 = 0x62;
  public const ushort kVK_F3 = 0x63;
  public const ushort kVK_F8 = 0x64;
  public const ushort kVK_F9 = 0x65;
  public const ushort kVK_F11 = 0x67;
  public const ushort kVK_F13 = 0x69;
  public const ushort kVK_F16 = 0x6A;
  public const ushort kVK_F14 = 0x6B;
  public const ushort kVK_F10 = 0x6D;
  public const ushort kVK_F12 = 0x6F;
  public const ushort kVK_F15 = 0x71;
  public const ushort kVK_Help = 0x72;
  public const ushort kVK_Home = 0x73;
  public const ushort kVK_PageUp = 0x74;
  public const ushort kVK_ForwardDelete = 0x75;
  public const ushort kVK_F4 = 0x76;
  public const ushort kVK_End = 0x77;
  public const ushort kVK_F2 = 0x78;
  public const ushort kVK_PageDown = 0x79;
  public const ushort kVK_F1 = 0x7A;
  public const ushort kVK_LeftArrow = 0x7B;
  public const ushort kVK_RightArrow = 0x7C;
  public const ushort kVK_DownArrow = 0x7D;
  public const ushort kVK_UpArrow = 0x7E;

  // CGEvent creation
  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventSourceCreate")]
  public static extern nint CGEventSourceCreate(uint stateID);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventCreateKeyboardEvent")]
  public static extern nint CGEventCreateKeyboardEvent(nint source, ushort virtualKey, bool keyDown);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventCreateMouseEvent")]
  public static extern nint CGEventCreateMouseEvent(nint source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventCreateScrollWheelEvent")]
  public static extern nint CGEventCreateScrollWheelEvent(nint source, uint units, uint wheelCount, int wheel1, int wheel2);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventPost")]
  public static extern void CGEventPost(uint tap, nint @event);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventPostToPid")]
  public static extern void CGEventPostToPid(int pid, nint @event);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventSetIntegerValueField")]
  public static extern void CGEventSetIntegerValueField(nint @event, uint field, long value);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventSetFlags")]
  public static extern void CGEventSetFlags(nint @event, ulong flags);

  // Mouse position
  [DllImport(CoreGraphicsFramework, EntryPoint = "CGWarpMouseCursorPosition")]
  public static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGDisplayMoveCursorToPoint")]
  public static extern int CGDisplayMoveCursorToPoint(uint display, CGPoint point);

  // Text input
  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventCreateKeyboardEvent")]
  public static extern nint CGEventCreateKeyboardEventWithUnicode(nint source, ushort virtualKey, bool keyDown);

  [DllImport(CoreGraphicsFramework, EntryPoint = "CGEventKeyboardSetUnicodeString")]
  public static extern void CGEventKeyboardSetUnicodeString(nint @event, nint stringLength, char[] unicodeString);

  // Memory management
  [DllImport(CoreGraphicsFramework, EntryPoint = "CFRelease")]
  public static extern void CFRelease(nint cf);


  // Constants for event taps
  public const uint kCGHIDEventTap = 0;
  public const uint kCGSessionEventTap = 1;
  public const uint kCGAnnotatedSessionEventTap = 2;

  // Event field constants
  public const uint kCGKeyboardEventKeycode = 9;
  public const uint kCGScrollWheelEventDeltaAxis1 = 11;
  public const uint kCGScrollWheelEventDeltaAxis2 = 12;
  public const uint kCGMouseEventClickState = 1;

  // Modifier flags
  public const ulong kCGEventFlagMaskAlphaShift = 0x00010000;
  public const ulong kCGEventFlagMaskShift = 0x00020000;
  public const ulong kCGEventFlagMaskControl = 0x00040000;
  public const ulong kCGEventFlagMaskAlternate = 0x00080000;
  public const ulong kCGEventFlagMaskCommand = 0x00100000;
  public const ulong kCGEventFlagMaskHelp = 0x00400000;
  public const ulong kCGEventFlagMaskSecondaryFn = 0x00800000;
  public const ulong kCGEventFlagMaskNumericPad = 0x00200000;
  public const ulong kCGEventFlagMaskNonCoalesced = 0x00000100;

  // Scroll wheel units
  public const uint kCGScrollEventUnitPixel = 0;
  public const uint kCGScrollEventUnitLine = 1;

  [StructLayout(LayoutKind.Sequential)]
  public struct CGPoint
  {
    public double X;
    public double Y;

    public CGPoint(double x, double y)
    {
      X = x;
      Y = y;
    }
  }
}
