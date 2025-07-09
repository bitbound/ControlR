namespace ControlR.BackgroundShell;

partial class StartMenu
{
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
      this._treeView = new System.Windows.Forms.TreeView();
      this.SuspendLayout();
      // 
      // _treeView
      // 
      this._treeView.BackColor = System.Drawing.SystemColors.Menu;
      this._treeView.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
      this._treeView.Dock = System.Windows.Forms.DockStyle.Fill;
      this._treeView.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
      this._treeView.FullRowSelect = true;
      this._treeView.HideSelection = false;
      this._treeView.Location = new System.Drawing.Point(0, 0);
      this._treeView.Name = "_treeView";
      this._treeView.Size = new System.Drawing.Size(300, 400);
      this._treeView.TabIndex = 0;
      this._treeView.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler(this.TreeView_BeforeExpand);
      this._treeView.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.TreeView_NodeMouseClick);
      // 
      // StartMenu
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(300, 400);
      this.ControlBox = false;
      this.Controls.Add(this._treeView);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "StartMenu";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
      this.TopMost = true;
      this.Deactivate += new System.EventHandler(this.StartMenu_Deactivate);
      this.ResumeLayout(false);

  }

  #endregion

  private System.Windows.Forms.TreeView _treeView;
}