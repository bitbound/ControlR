using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

public static class LibGtk
{
  private const string GtkLibraryName = "libgtk-3.so.0";
  private const string GdkLibraryName = "libgdk-3.so.0";

  [DllImport(GtkLibraryName)]
  public static extern bool gtk_init_check(ref int argc, nint argv);

  [DllImport(GtkLibraryName)]
  public static extern nint gtk_clipboard_get(nint selection);

  [DllImport(GtkLibraryName)]
  public static extern void gtk_clipboard_set_text(nint clipboard, string text, int len);

  [DllImport(GtkLibraryName)]
  public static extern nint gtk_clipboard_wait_for_text(nint clipboard);

  [DllImport(GtkLibraryName)]
  public static extern void gtk_clipboard_store(nint clipboard);

  [DllImport(GdkLibraryName)]
  public static extern nint gdk_atom_intern(string atom_name, bool only_if_exists);

  [DllImport("libglib-2.0.so.0")]
  public static extern void g_free(nint mem);

  // GDK selection atoms
  public static nint GDK_SELECTION_CLIPBOARD => gdk_atom_intern("CLIPBOARD", false);
  public static nint GDK_SELECTION_PRIMARY => gdk_atom_intern("PRIMARY", false);
}