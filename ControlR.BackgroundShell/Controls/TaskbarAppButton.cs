using System.ComponentModel;
using System.Diagnostics;
using ControlR.BackgroundShell.Helpers;

namespace ControlR.BackgroundShell.Controls;

internal class TaskbarAppButton : TaskbarButton
{
  private static readonly string _mmcPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.System),
    "mmc.exe");

  private Icon? _appIcon;
  private Process? _process;

  public TaskbarAppButton()
  {
    BackgroundImageLayout = ImageLayout.Center;
  }

  [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
  [Description("Path to the application executable.")]
  public string AppPath { get; set; } = string.Empty;

  [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
  [Description("Title of the application window.")]
  public string WindowTitle { get; set; } = string.Empty;

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      _appIcon?.Dispose();
    }
    base.Dispose(disposing);
  }

  protected override void OnClick(EventArgs e)
  {
    base.OnClick(e);
    if (string.IsNullOrWhiteSpace(AppPath) || !File.Exists(AppPath))
    {
      MessageBox.Show("Application path is invalid.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      return;
    }

    try
    {
      var windowHandle = GetWindowHandle();

      if (windowHandle != IntPtr.Zero)
      {
        Win32Interop.FocusWindow(windowHandle);
        return;
      }

      if (AppPath.EndsWith(".msc"))
      {
        _process = Process.Start(_mmcPath, AppPath);
      }
      else
      {
        _process = Process.Start(AppPath);
      }
    }
    catch (Exception ex)
    {
      MessageBox.Show($"Failed to start application: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }

  protected override void OnPaint(PaintEventArgs pevent)
  {
    base.OnPaint(pevent);

    if (!File.Exists(AppPath))
    {
      DrawGrayX(pevent);
      return;
    }

    try
    {
      _appIcon ??= Icon.ExtractAssociatedIcon(AppPath);
      if (_appIcon != null)
      {
        pevent.Graphics.DrawIcon(_appIcon, new Rectangle(8, 8, Width - 16, Height - 16));
      }
      else
      {
        DrawGrayX(pevent);
      }

      var windowHandle = GetWindowHandle();
      if (windowHandle == IntPtr.Zero)
      {
        // No window handle means the application is not running or not found
        return;
      }

      var isFocused = Win32Interop.IsWindowFocused(windowHandle);
      DrawOpenIndicator(pevent, isFocused);
    }
    catch
    {
      MessageBox.Show(
        $"Failed to load application icon: {AppPath}",
        "Error",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    }
  }

  private void DrawGrayX(PaintEventArgs pevent)
  {
    using var pen = new Pen(Color.Gray, 2);
    var graphics = pevent.Graphics;
    var margin = 8; // Same margin as used for icon drawing

    // Draw X from top-left to bottom-right
    graphics.DrawLine(pen, margin, margin, Width - margin, Height - margin);

    // Draw X from top-right to bottom-left
    graphics.DrawLine(pen, Width - margin, margin, margin, Height - margin);
  }

  private IntPtr GetWindowHandle()
  {
    var windowHandle = IntPtr.Zero;
    if (!string.IsNullOrWhiteSpace(WindowTitle))
    {
      windowHandle = Win32Interop.FindWindow(WindowTitle);
    }

    if (windowHandle == IntPtr.Zero && _process?.HasExited == false)
    {
      windowHandle = _process.MainWindowHandle;
    }
    return windowHandle;
  }
}
