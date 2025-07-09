using ControlR.BackgroundShell.Helpers;

namespace ControlR.BackgroundShell;

public partial class Shell : Form
{
  private const int WM_USER_CAPTURE_END = 0x8002;
  private const int WM_USER_CAPTURE_START = 0x8001;

  private bool _captureInProgress = false;
  private StartMenu? _startMenu;
  private FileExplorerDialog? _fileExplorer;


  public Shell()
  {
    InitializeComponent();
    Left = 0;
    if (Screen.PrimaryScreen is not null)
    {
      Top = Screen.PrimaryScreen.Bounds.Height - Height;
      Width = Screen.PrimaryScreen.Bounds.Width;
    }

    _powerShellButton.AppPath =
      Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    _registryEditorButton.AppPath =
      Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.Windows),
      "regedit.exe");

    _cmdButton.AppPath =
      Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.System),
      "cmd.exe");

    _computerMgmtButton.AppPath =
      Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.System),
      "compmgmt.msc");

    _eventViewerButton.AppPath =
      Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.System),
      "eventvwr.msc");

    _servicesButton.AppPath =
      Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.System),
      "services.msc");

    _perfMonButton.AppPath =
      Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.System),
      "perfmon.msc");

    _firewallButton.AppPath =
      Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.System),
      "WF.msc");

    _notepadButton.AppPath =
      Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.Windows),
      "notepad.exe");

    _fileExplorerButton.GetFormFunc = () => _fileExplorer;

    Win32Interop.CloseOtherProcesses();
  }

  protected override void OnPaint(PaintEventArgs e)
  {
    if (_captureInProgress)
    {
      // During capture, ensure all child controls are fully painted
      foreach (Control control in Controls)
      {
        if (control.Visible)
        {
          control.Update();
        }
      }
    }

    base.OnPaint(e);
  }

  protected override void WndProc(ref Message m)
  {
    switch (m.Msg)
    {
      case WM_USER_CAPTURE_START:
        // Suspend all animations/updates during capture
        _captureInProgress = true;
        SuspendLayout();

        // Suspend layout for all child controls
        foreach (Control ctrl in Controls)
        {
          ctrl.SuspendLayout();
        }

        // Force immediate update and refresh
        Update();
        Refresh();

        // Ensure all child controls are updated
        foreach (Control ctrl in Controls)
        {
          ctrl.Update();
          ctrl.Refresh();
        }

        // Signal that we're ready for capture
        m.Result = new IntPtr(1);
        return;

      case WM_USER_CAPTURE_END:
        // Resume animations/updates after capture
        _captureInProgress = false;

        // Resume layout for all child controls
        foreach (Control ctrl in Controls)
        {
          ctrl.ResumeLayout();
        }

        ResumeLayout();
        m.Result = new IntPtr(1);
        return;
    }

    base.WndProc(ref m);
  }

  private void FileExplorerButton_Click(object sender, EventArgs e)
  {
    if (_fileExplorer is null || _fileExplorer.IsDisposed)
    {
      _fileExplorer = new FileExplorerDialog
      {
        InitialDirectory = Path.GetPathRoot(Environment.SystemDirectory),
        Title = "ControlR File Explorer",
        TopMost = false
      };

      _fileExplorer.FormClosed += (s, args) =>
      {
        _fileExplorer = null;
      };
      _fileExplorer.Show();
      return;
    }

    _fileExplorer.BringToFront();
    _fileExplorer.Focus();
  }

  private void StartButton_Click(object sender, System.EventArgs e)
  {
    if (_startMenu == null || _startMenu.IsDisposed)
    {
      _startMenu = new StartMenu
      {
        TopMost = true
      };
      _startMenu.FormClosed += (s, args) => _startMenu = null;

      // Position the start menu above the start button
      var startButtonLocation = _startButton.PointToScreen(Point.Empty);
      _startMenu.StartPosition = FormStartPosition.Manual;
      _startMenu.Left = startButtonLocation.X;
      _startMenu.Top = startButtonLocation.Y - _startMenu.Height;

      // Ensure the menu stays on screen
      if (_startMenu.Top < 0)
        _startMenu.Top = 0;

      _startMenu.Show();
    }
    else
    {
      _startMenu.Close();
    }
  }
}