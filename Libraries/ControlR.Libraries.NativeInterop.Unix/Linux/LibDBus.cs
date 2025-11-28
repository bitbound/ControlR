using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

/// <summary>
/// DBus native interop for communicating with system services.
/// Used primarily for XDG Desktop Portal on Wayland.
/// </summary>
public static partial class LibDBus
{
  // Bus types
  public const int DBUS_BUS_SESSION = 0;
  public const int DBUS_BUS_STARTER = 2;
  public const int DBUS_BUS_SYSTEM = 1;
  public const int DBUS_ERROR_FAILED = -1;
  // DBus error codes
  public const int DBUS_ERROR_SUCCESS = 0;
  public const int DBUS_TYPE_ARRAY = ((int)'a');
  public const int DBUS_TYPE_BOOLEAN = ((int)'b');
  public const int DBUS_TYPE_DICT_ENTRY = ((int)'e');
  public const int DBUS_TYPE_DOUBLE = ((int)'d');
  public const int DBUS_TYPE_INT32 = ((int)'i');
  public const int DBUS_TYPE_INT64 = ((int)'x');
  // DBus types
  public const int DBUS_TYPE_INVALID = 0;
  public const int DBUS_TYPE_OBJECT_PATH = ((int)'o');
  public const int DBUS_TYPE_SIGNATURE = ((int)'g');
  public const int DBUS_TYPE_STRING = ((int)'s');
  public const int DBUS_TYPE_STRUCT = ((int)'r');
  public const int DBUS_TYPE_UINT32 = ((int)'u');
  public const int DBUS_TYPE_UINT64 = ((int)'t');
  public const int DBUS_TYPE_UNIX_FD = ((int)'h');
  public const int DBUS_TYPE_VARIANT = ((int)'v');

  private const string LibraryName = "libdbus-1.so.3";

  [LibraryImport(LibraryName)]
  public static partial nint dbus_bus_get(int type, ref DBusError error);
  [LibraryImport(LibraryName)]
  public static partial void dbus_connection_flush(nint connection);
  [LibraryImport(LibraryName)]
  public static partial nint dbus_connection_send_with_reply_and_block(
    nint connection,
    nint message,
    int timeout_milliseconds,
    ref DBusError error);
  [LibraryImport(LibraryName)]
  public static partial void dbus_connection_unref(nint connection);
  [LibraryImport(LibraryName)]
  public static partial void dbus_error_free(ref DBusError error);
  [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
  public static partial void dbus_error_init(ref DBusError error);
  [LibraryImport(LibraryName)]
  [return: MarshalAs(UnmanagedType.I1)]
  public static partial bool dbus_error_is_set(ref DBusError error);
  [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
  [return: MarshalAs(UnmanagedType.I1)]
  public static partial bool dbus_message_append_args(
    nint message,
    int first_arg_type,
    nint first_arg_value,
    int terminator);
  [LibraryImport(LibraryName)]
  public static partial int dbus_message_iter_get_arg_type(ref DBusMessageIter iter);
  [LibraryImport(LibraryName)]
  public static partial void dbus_message_iter_get_basic(ref DBusMessageIter iter, out nint value);
  [LibraryImport(LibraryName)]
  [return: MarshalAs(UnmanagedType.I1)]
  public static partial bool dbus_message_iter_init(nint message, ref DBusMessageIter iter);
  [LibraryImport(LibraryName)]
  [return: MarshalAs(UnmanagedType.I1)]
  public static partial bool dbus_message_iter_next(ref DBusMessageIter iter);
  [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
  public static partial nint dbus_message_new_method_call(
    string? bus_name,
    string object_path,
    string? @interface,
    string method);
  [LibraryImport(LibraryName)]
  public static partial void dbus_message_unref(nint message);

  [StructLayout(LayoutKind.Sequential)]
  public struct DBusError
  {
    public nint name;
    public nint message;
    public uint dummy1;
    public uint dummy2;
    public uint dummy3;
    public uint dummy4;
    public uint dummy5;
    public nint padding1;
  }
  [StructLayout(LayoutKind.Sequential)]
  public struct DBusMessageIter
  {
    public nint dummy1;
    public nint dummy2;
    public uint dummy3;
    public int dummy4;
    public int dummy5;
    public int dummy6;
    public int dummy7;
    public int dummy8;
    public int dummy9;
    public int dummy10;
    public int dummy11;
    public int pad1;
    public nint pad2;
    public nint pad3;
  }
}
