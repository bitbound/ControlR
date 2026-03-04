using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Input;

namespace ControlR.Viewer.Avalonia.Native;

internal static class NativeHelperWindows
{
  public const int VK_CONTROL = 0x11;
  public const int VK_LWIN = 0x5B;
  public const int VK_MENU = 0x12;
  public const int VK_RWIN = 0x5C;
  public const int VK_SHIFT = 0x10;

  private const int WH_KEYBOARD_LL = 13;
  private const int WM_KEYDOWN = 0x0100;
  private const int WM_KEYUP = 0x0101;
  private const int WM_SYSKEYDOWN = 0x0104;
  private const int WM_SYSKEYUP = 0x0105;

  private static readonly IntPtr _suppressEventResult = new(1);

  public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

  public static KeyModifiers GetModifiersFromNative()
  {
    var mods = KeyModifiers.None;

    if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
    {
      mods |= KeyModifiers.Control;
    }

    if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
    {
      mods |= KeyModifiers.Shift;
    }

    if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
    {
      mods |= KeyModifiers.Alt;
    }

    if (((GetAsyncKeyState(VK_LWIN) | GetAsyncKeyState(VK_RWIN)) & 0x8000) != 0)
    {
      mods |= KeyModifiers.Meta;
    }

    return mods;
  }

  public static IDisposable? InstallKeyboardHook(Func<int, bool, bool, bool> callback)
  {
    ArgumentNullException.ThrowIfNull(callback);

    using var curProcess = Process.GetCurrentProcess();
    var moduleName = curProcess.MainModule?.ModuleName;
    if (string.IsNullOrWhiteSpace(moduleName))
    {
      return null;
    }

    var moduleHandle = GetModuleHandle(moduleName);
    var registration = new KeyboardHookRegistration(callback);
    if (!registration.TryInstall(moduleHandle))
    {
      registration.Dispose();
      return null;
    }

    return registration;
  }

  [DllImport("user32.dll", SetLastError = true)]
  private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

  [DllImport("user32.dll")]
  private static extern short GetAsyncKeyState(int vKey);

  [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  private static extern IntPtr GetModuleHandle(string lpModuleName);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

  [DllImport("user32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool UnhookWindowsHookEx(IntPtr hhk);

  [StructLayout(LayoutKind.Sequential)]
  private struct KBDLLHOOKSTRUCT
  {
    public uint vkCode;
    public uint scanCode;
    public uint flags;
    public uint time;
    public UIntPtr dwExtraInfo;
  }

  private sealed class KeyboardHookRegistration : IDisposable
  {
    private readonly Func<int, bool, bool, bool> _callback;
    private readonly LowLevelKeyboardProc _hookProc;

    private int _disposeSignaled;
    private IntPtr _hookId;

    public KeyboardHookRegistration(Func<int, bool, bool, bool> callback)
    {
      _callback = callback;
      _hookProc = HookCallback;
    }

    public void Dispose()
    {
      if (Interlocked.Exchange(ref _disposeSignaled, 1) != 0)
      {
        return;
      }

      var hookId = Interlocked.Exchange(ref _hookId, IntPtr.Zero);
      if (hookId == IntPtr.Zero)
      {
        return;
      }

      UnhookWindowsHookEx(hookId);
    }

    public bool TryInstall(IntPtr moduleHandle)
    {
      _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, moduleHandle, 0);
      return _hookId != IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
      if (nCode < 0)
      {
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
      }

      try
      {
        var message = (int)wParam;
        var isDown = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
        var isUp = message == WM_KEYUP || message == WM_SYSKEYUP;
        var isSystemKey = message == WM_SYSKEYDOWN || message == WM_SYSKEYUP;

        if (isDown || isUp)
        {
          var keyboardData = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
          var virtualKey = (int)keyboardData.vkCode;
          var shouldSuppress = _callback(virtualKey, isDown, isSystemKey);
          if (shouldSuppress)
          {
            return _suppressEventResult;
          }
        }
      }
      catch
      {
      }

      return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
  }
}
