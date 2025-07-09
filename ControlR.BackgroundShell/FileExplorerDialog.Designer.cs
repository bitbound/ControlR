using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlR.BackgroundShell;
partial class FileExplorerDialog
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
  private void InitializeComponent()
  {
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FileExplorerDialog));
      this.SuspendLayout();
      // 
      // FileExplorerDialog
      // 
      this.ClientSize = new System.Drawing.Size(457, 320);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.Name = "FileExplorerDialog";
      this.ResumeLayout(false);

  }
  private ToolStrip _toolStrip = null!;
  private ToolStripButton _backButton = null!;
  private ToolStripButton _forwardButton = null!;
  private ToolStripButton _upButton = null!;
  private ToolStripTextBox _addressBar = null!;
  private ToolStripButton _refreshButton = null!;
  private ToolStripDropDownButton _viewButton = null!;

  private SplitContainer _mainSplitContainer = null!;
  private TreeView _folderTreeView = null!;
  private ListView _fileListView = null!;
  private StatusStrip _statusStrip = null!;
  private ToolStripStatusLabel _statusLabel = null!;
  private ToolStripStatusLabel _itemCountLabel = null!;

  private Button _closeButton = null!;
  private ImageList _treeImageList = null!;
  private ImageList _listImageList = null!;
}
