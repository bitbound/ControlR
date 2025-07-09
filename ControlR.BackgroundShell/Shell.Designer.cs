using ControlR.BackgroundShell.Controls;

namespace ControlR.BackgroundShell
{
  partial class Shell
  {
    
    private TaskbarButton _startButton;
    private System.Windows.Forms.ToolTip textToolTip;
    private Controls.TaskbarAppButton _powerShellButton;
    private TaskbarAppButton _registryEditorButton;
    private TaskbarAppButton _cmdButton;
    private TaskbarAppButton _eventViewerButton;
    private TaskbarAppButton _servicesButton;
    private TaskbarAppButton _perfMonButton;
    private TaskbarAppButton _firewallButton;
    private TaskbarAppButton _computerMgmtButton;
    private TaskbarAppButton _notepadButton;
    private TaskbarFormButton _fileExplorerButton;
    private SystemClock _systemClock;
    
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.components = new System.ComponentModel.Container();
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Shell));
      this.textToolTip = new System.Windows.Forms.ToolTip(this.components);
      this._notepadButton = new ControlR.BackgroundShell.Controls.TaskbarAppButton();
      this._computerMgmtButton = new ControlR.BackgroundShell.Controls.TaskbarAppButton();
      this._firewallButton = new ControlR.BackgroundShell.Controls.TaskbarAppButton();
      this._perfMonButton = new ControlR.BackgroundShell.Controls.TaskbarAppButton();
      this._servicesButton = new ControlR.BackgroundShell.Controls.TaskbarAppButton();
      this._eventViewerButton = new ControlR.BackgroundShell.Controls.TaskbarAppButton();
      this._cmdButton = new ControlR.BackgroundShell.Controls.TaskbarAppButton();
      this._registryEditorButton = new ControlR.BackgroundShell.Controls.TaskbarAppButton();
      this._powerShellButton = new ControlR.BackgroundShell.Controls.TaskbarAppButton();
      this._startButton = new ControlR.BackgroundShell.Controls.TaskbarButton();
      this._systemClock = new ControlR.BackgroundShell.Controls.SystemClock();
      this._fileExplorerButton = new ControlR.BackgroundShell.Controls.TaskbarFormButton();
      this.SuspendLayout();
      // 
      // _notepadButton
      // 
      this._notepadButton.AppPath = "C:\\Windows\\notepad.exe";
      this._notepadButton.BackColor = System.Drawing.Color.Transparent;
      this._notepadButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
      this._notepadButton.FlatAppearance.BorderSize = 0;
      this._notepadButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._notepadButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
      this._notepadButton.ForeColor = System.Drawing.Color.White;
      this._notepadButton.Location = new System.Drawing.Point(747, 0);
      this._notepadButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._notepadButton.Name = "_notepadButton";
      this._notepadButton.Size = new System.Drawing.Size(67, 62);
      this._notepadButton.TabIndex = 12;
      this.textToolTip.SetToolTip(this._notepadButton, "Notepad");
      this._notepadButton.UseVisualStyleBackColor = false;
      // 
      // _computerMgmtButton
      // 
      this._computerMgmtButton.AppPath = "C:\\Windows\\System32\\compmgmt.msc";
      this._computerMgmtButton.BackColor = System.Drawing.Color.Transparent;
      this._computerMgmtButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
      this._computerMgmtButton.FlatAppearance.BorderSize = 0;
      this._computerMgmtButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._computerMgmtButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
      this._computerMgmtButton.ForeColor = System.Drawing.Color.White;
      this._computerMgmtButton.Location = new System.Drawing.Point(299, 0);
      this._computerMgmtButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._computerMgmtButton.Name = "_computerMgmtButton";
      this._computerMgmtButton.Size = new System.Drawing.Size(67, 62);
      this._computerMgmtButton.TabIndex = 11;
      this._computerMgmtButton.WindowTitle = "Computer Management";
      this.textToolTip.SetToolTip(this._computerMgmtButton, "Computer Management");
      this._computerMgmtButton.UseVisualStyleBackColor = false;
      // 
      // _firewallButton
      // 
      this._firewallButton.AppPath = "C:\\Windows\\System32\\WF.msc";
      this._firewallButton.BackColor = System.Drawing.Color.Transparent;
      this._firewallButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
      this._firewallButton.FlatAppearance.BorderSize = 0;
      this._firewallButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._firewallButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
      this._firewallButton.ForeColor = System.Drawing.Color.White;
      this._firewallButton.Location = new System.Drawing.Point(672, 0);
      this._firewallButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._firewallButton.Name = "_firewallButton";
      this._firewallButton.Size = new System.Drawing.Size(67, 62);
      this._firewallButton.TabIndex = 10;
      this._firewallButton.WindowTitle = "Windows Defender Firewall with Advanced Security";
      this.textToolTip.SetToolTip(this._firewallButton, "Firewall");
      this._firewallButton.UseVisualStyleBackColor = false;
      // 
      // _perfMonButton
      // 
      this._perfMonButton.AppPath = "C:\\Windows\\System32\\perfmon.msc";
      this._perfMonButton.BackColor = System.Drawing.Color.Transparent;
      this._perfMonButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
      this._perfMonButton.FlatAppearance.BorderSize = 0;
      this._perfMonButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._perfMonButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
      this._perfMonButton.ForeColor = System.Drawing.Color.White;
      this._perfMonButton.Location = new System.Drawing.Point(597, 0);
      this._perfMonButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._perfMonButton.Name = "_perfMonButton";
      this._perfMonButton.Size = new System.Drawing.Size(67, 62);
      this._perfMonButton.TabIndex = 9;
      this._perfMonButton.WindowTitle = "Performance Monitor";
      this.textToolTip.SetToolTip(this._perfMonButton, "Performance Monitor");
      this._perfMonButton.UseVisualStyleBackColor = false;
      // 
      // _servicesButton
      // 
      this._servicesButton.AppPath = "C:\\Windows\\System32\\services.msc";
      this._servicesButton.BackColor = System.Drawing.Color.Transparent;
      this._servicesButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
      this._servicesButton.FlatAppearance.BorderSize = 0;
      this._servicesButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._servicesButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
      this._servicesButton.ForeColor = System.Drawing.Color.White;
      this._servicesButton.Location = new System.Drawing.Point(523, 0);
      this._servicesButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._servicesButton.Name = "_servicesButton";
      this._servicesButton.Size = new System.Drawing.Size(67, 62);
      this._servicesButton.TabIndex = 8;
      this._servicesButton.WindowTitle = "Services";
      this.textToolTip.SetToolTip(this._servicesButton, "Services");
      this._servicesButton.UseVisualStyleBackColor = false;
      // 
      // _eventViewerButton
      // 
      this._eventViewerButton.AppPath = "C:\\Windows\\System32\\eventvwr.msc";
      this._eventViewerButton.BackColor = System.Drawing.Color.Transparent;
      this._eventViewerButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
      this._eventViewerButton.FlatAppearance.BorderSize = 0;
      this._eventViewerButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._eventViewerButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
      this._eventViewerButton.ForeColor = System.Drawing.Color.White;
      this._eventViewerButton.Location = new System.Drawing.Point(448, 0);
      this._eventViewerButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._eventViewerButton.Name = "_eventViewerButton";
      this._eventViewerButton.Size = new System.Drawing.Size(67, 62);
      this._eventViewerButton.TabIndex = 7;
      this._eventViewerButton.WindowTitle = "Event Viewer";
      this.textToolTip.SetToolTip(this._eventViewerButton, "Event Viewer");
      this._eventViewerButton.UseVisualStyleBackColor = false;
      // 
      // _cmdButton
      // 
      this._cmdButton.AppPath = "C:\\Windows\\System32\\cmd.exe";
      this._cmdButton.BackColor = System.Drawing.Color.Transparent;
      this._cmdButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
      this._cmdButton.FlatAppearance.BorderSize = 0;
      this._cmdButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._cmdButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
      this._cmdButton.ForeColor = System.Drawing.Color.White;
      this._cmdButton.Location = new System.Drawing.Point(224, 0);
      this._cmdButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._cmdButton.Name = "_cmdButton";
      this._cmdButton.Size = new System.Drawing.Size(67, 62);
      this._cmdButton.TabIndex = 5;
      this.textToolTip.SetToolTip(this._cmdButton, "Command Line");
      this._cmdButton.UseVisualStyleBackColor = false;
      // 
      // _registryEditorButton
      // 
      this._registryEditorButton.AppPath = "C:\\Windows\\regedit.exe";
      this._registryEditorButton.BackColor = System.Drawing.Color.Transparent;
      this._registryEditorButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
      this._registryEditorButton.FlatAppearance.BorderSize = 0;
      this._registryEditorButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._registryEditorButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
      this._registryEditorButton.ForeColor = System.Drawing.Color.White;
      this._registryEditorButton.Location = new System.Drawing.Point(373, 0);
      this._registryEditorButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._registryEditorButton.Name = "_registryEditorButton";
      this._registryEditorButton.Size = new System.Drawing.Size(67, 62);
      this._registryEditorButton.TabIndex = 4;
      this.textToolTip.SetToolTip(this._registryEditorButton, "Registry Editor");
      this._registryEditorButton.UseVisualStyleBackColor = false;
      // 
      // _powerShellButton
      // 
      this._powerShellButton.AppPath = "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe";
      this._powerShellButton.BackColor = System.Drawing.Color.Transparent;
      this._powerShellButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
      this._powerShellButton.FlatAppearance.BorderSize = 0;
      this._powerShellButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._powerShellButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
      this._powerShellButton.ForeColor = System.Drawing.Color.White;
      this._powerShellButton.Location = new System.Drawing.Point(149, 0);
      this._powerShellButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._powerShellButton.Name = "_powerShellButton";
      this._powerShellButton.Size = new System.Drawing.Size(67, 62);
      this._powerShellButton.TabIndex = 1;
      this.textToolTip.SetToolTip(this._powerShellButton, "PowerShell");
      this._powerShellButton.UseVisualStyleBackColor = false;
      // 
      // _startButton
      // 
      this._startButton.BackColor = System.Drawing.Color.Transparent;
      this._startButton.BackgroundImage = global::ControlR.BackgroundShell.Properties.Resources.StartButton40x40;
      this._startButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
      this._startButton.FlatAppearance.BorderSize = 0;
      this._startButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._startButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
      this._startButton.ForeColor = System.Drawing.Color.White;
      this._startButton.Location = new System.Drawing.Point(0, 0);
      this._startButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._startButton.Name = "_startButton";
      this._startButton.Size = new System.Drawing.Size(67, 62);
      this._startButton.TabIndex = 0;
      this.textToolTip.SetToolTip(this._startButton, "Start");
      this._startButton.UseVisualStyleBackColor = false;
      this._startButton.Click += new System.EventHandler(this.StartButton_Click);
      // 
      // _systemClock
      // 
      this._systemClock.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this._systemClock.ForeColor = System.Drawing.Color.White;
      this._systemClock.Location = new System.Drawing.Point(1156, 0);
      this._systemClock.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._systemClock.Name = "_systemClock";
      this._systemClock.Size = new System.Drawing.Size(193, 62);
      this._systemClock.TabIndex = 14;
      // 
      // _fileExplorerButton
      // 
      this._fileExplorerButton.BackColor = System.Drawing.Color.Transparent;
      this._fileExplorerButton.BackgroundImage = global::ControlR.BackgroundShell.Properties.Resources.FolderIcon40x40;
      this._fileExplorerButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
      this._fileExplorerButton.FlatAppearance.BorderSize = 0;
      this._fileExplorerButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._fileExplorerButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
      this._fileExplorerButton.ForeColor = System.Drawing.Color.White;
      this._fileExplorerButton.Location = new System.Drawing.Point(75, 0);
      this._fileExplorerButton.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this._fileExplorerButton.Name = "_fileExplorerButton";
      this._fileExplorerButton.Size = new System.Drawing.Size(67, 62);
      this._fileExplorerButton.TabIndex = 13;
      this._fileExplorerButton.UseVisualStyleBackColor = false;
      this._fileExplorerButton.Click += new System.EventHandler(this.FileExplorerButton_Click);
      // 
      // Shell
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
      this.ClientSize = new System.Drawing.Size(1365, 62);
      this.Controls.Add(this._systemClock);
      this.Controls.Add(this._fileExplorerButton);
      this.Controls.Add(this._notepadButton);
      this.Controls.Add(this._computerMgmtButton);
      this.Controls.Add(this._firewallButton);
      this.Controls.Add(this._perfMonButton);
      this.Controls.Add(this._servicesButton);
      this.Controls.Add(this._eventViewerButton);
      this.Controls.Add(this._cmdButton);
      this.Controls.Add(this._registryEditorButton);
      this.Controls.Add(this._powerShellButton);
      this.Controls.Add(this._startButton);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
      this.Name = "Shell";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
      this.Text = "ControlR Shell";
      this.TopMost = true;
      this.ResumeLayout(false);

    }

    #endregion

  }
}

