using ControlR.BackgroundShell.Properties;
using System.Diagnostics;

namespace ControlR.BackgroundShell;

public partial class FileExplorerDialog : Form
{
  private readonly Stack<string> _backHistory = new();
  private readonly Stack<string> _forwardHistory = new();
  private string _currentPath = string.Empty;
  private readonly Dictionary<string, TreeNode> _pathToNodeMap = [];

  public string InitialDirectory { get; set; } = Path.GetPathRoot(Environment.SystemDirectory);
  public string Title { get; set; } = "ControlR File Explorer";

  public FileExplorerDialog()
  {
    InitializeComponent();
    Text = Title;
    Size = new Size(800, 600);
    MinimumSize = new Size(800, 600);
    StartPosition = FormStartPosition.CenterScreen;
    FormBorderStyle = FormBorderStyle.Sizable;
    TopMost = false;
    CreateControls();
    LayoutControls();
    InitializeImageLists();
    LoadInitialDirectory();
  }

  protected void CreateControls()
  {
    // Create toolbar
    _toolStrip = new ToolStrip
    {
      GripStyle = ToolStripGripStyle.Hidden,
      ImageScalingSize = new Size(16, 16)
    };

    _backButton = new ToolStripButton("←")
    {
      ToolTipText = "Back",
      Enabled = false
    };
    _backButton.Click += BackButton_Click;

    _forwardButton = new ToolStripButton("→")
    {
      ToolTipText = "Forward",
      Enabled = false
    };
    _forwardButton.Click += ForwardButton_Click;

    _upButton = new ToolStripButton("↑")
    {
      ToolTipText = "Up one level"
    };
    _upButton.Click += UpButton_Click;

    _addressBar = new ToolStripTextBox
    {
      AutoSize = false,
      Width = 400
    };
    _addressBar.KeyDown += AddressBar_KeyDown;

    _refreshButton = new ToolStripButton("⟳")
    {
      ToolTipText = "Refresh"
    };
    _refreshButton.Click += RefreshButton_Click;

    _viewButton = new ToolStripDropDownButton("View")
    {
      ToolTipText = "Change view"
    };
    _viewButton.DropDownItems.AddRange(
    [
            new ToolStripMenuItem("Large Icons") { Tag = View.LargeIcon },
            new ToolStripMenuItem("Small Icons") { Tag = View.SmallIcon },
            new ToolStripMenuItem("List") { Tag = View.List },
            new ToolStripMenuItem("Details") { Tag = View.Details, Checked = true },
            new ToolStripMenuItem("Tile") { Tag = View.Tile }
    ]);

    foreach (ToolStripMenuItem item in _viewButton.DropDownItems)
    {
      item.Click += ViewMenuItem_Click;
    }

    _toolStrip.Items.AddRange(
    [
            _backButton,
            _forwardButton,
            new ToolStripSeparator(),
            _upButton,
            new ToolStripSeparator(),
            new ToolStripLabel("Address:"),
            _addressBar,
            _refreshButton,
            new ToolStripSeparator(),
            _viewButton
    ]);

    // Create main split container
    _mainSplitContainer = new SplitContainer
    {
      Dock = DockStyle.Fill,
      SplitterDistance = 250,
      FixedPanel = FixedPanel.Panel1
    };

    // Create folder tree view
    _folderTreeView = new TreeView
    {
      Dock = DockStyle.Fill,
      ShowLines = true,
      ShowPlusMinus = true,
      ShowRootLines = true,
      HideSelection = false
    };
    _folderTreeView.AfterSelect += FolderTreeView_AfterSelect;
    _folderTreeView.BeforeExpand += FolderTreeView_BeforeExpand;

    // Create file list view
    _fileListView = new ListView
    {
      Dock = DockStyle.Fill,
      View = View.Details,
      FullRowSelect = true,
      GridLines = true,
      MultiSelect = true,
      AllowColumnReorder = true
    };

    _fileListView.Columns.AddRange(
    [
            new ColumnHeader { Text = "Name", Width = 250 },
            new ColumnHeader { Text = "Date modified", Width = 150 },
            new ColumnHeader { Text = "Type", Width = 120 },
            new ColumnHeader { Text = "Size", Width = 100 }
        ]);

    _fileListView.ItemSelectionChanged += FileListView_ItemSelectionChanged;
    _fileListView.DoubleClick += FileListView_DoubleClick;
    _fileListView.KeyDown += FileListView_KeyDown;

    // Create status strip
    _statusStrip = new StatusStrip();
    _statusLabel = new ToolStripStatusLabel
    {
      Spring = true,
      TextAlign = ContentAlignment.MiddleLeft
    };
    _itemCountLabel = new ToolStripStatusLabel();
    _statusStrip.Items.AddRange([_statusLabel, _itemCountLabel]);

    // Create close button
    _closeButton = new Button
    {
      Text = "Close",
      Size = new Size(75, 30),
      Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
      DialogResult = DialogResult.OK
    };
    _closeButton.Click += (s, e) => Close();

    // Add controls to split container
    _mainSplitContainer.Panel1.Controls.Add(_folderTreeView);
    _mainSplitContainer.Panel2.Controls.Add(_fileListView);
  }

  protected void LayoutControls()
  {
    Controls.Add(_mainSplitContainer);
    Controls.Add(_toolStrip);
    Controls.Add(_statusStrip);
    Controls.Add(_closeButton);

    // Position close button
    _closeButton.Location = new Point(Width - _closeButton.Width - 15, Height - _closeButton.Height - 35);

    // Adjust main container to account for button
    _mainSplitContainer.Height = Height - _toolStrip.Height - _statusStrip.Height - _closeButton.Height - 15;
    _mainSplitContainer.Top = _toolStrip.Height;

    CancelButton = _closeButton;
  }

  private void InitializeImageLists()
  {
    _treeImageList = new ImageList { ImageSize = new Size(16, 16) };
    _listImageList = new ImageList { ImageSize = new Size(16, 16) };

    // Use Resources.FolderIcon for folders
    _treeImageList.Images.Add("folder", Resources.FolderIcon);
    _treeImageList.Images.Add("folderOpen", Resources.FolderIcon); // Optionally use a different icon for open
    _treeImageList.Images.Add("drive", Resources.DataStorageIcon);
    _treeImageList.Images.Add("pc", Resources.ComputerIcon);

    _listImageList.Images.Add("folder", Resources.FolderIcon);
    _listImageList.Images.Add("file", Resources.FileColorIcon);

    _folderTreeView.ImageList = _treeImageList;
    _fileListView.SmallImageList = _listImageList;
    _fileListView.LargeImageList = _listImageList;
  }

  private void LoadInitialDirectory()
  {
    try
    {
      LoadFolderTree();
      NavigateToPath(InitialDirectory);
    }
    catch (Exception ex)
    {
      MessageBox.Show($"Error loading initial directory: {ex.Message}", "Error",
          MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }

  private void LoadFolderTree()
  {
    _folderTreeView.Nodes.Clear();
    _pathToNodeMap.Clear();

    try
    {
      // Add "This PC" node
      var thisPcNode = new TreeNode("This PC")
      {
        Tag = "ThisPC",
        ImageKey = "pc",
        SelectedImageKey = "pc"
      };
      _folderTreeView.Nodes.Add(thisPcNode);

      // Add drives
      var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
      foreach (var drive in drives)
      {
        var driveNode = new TreeNode($"{drive.Name} ({drive.DriveType})")
        {
          Tag = drive.RootDirectory.FullName,
          ImageKey = "drive",
          SelectedImageKey = "drive"
        };

        _pathToNodeMap[drive.RootDirectory.FullName] = driveNode;
        thisPcNode.Nodes.Add(driveNode);

        // Add placeholder for lazy loading
        driveNode.Nodes.Add(new TreeNode("Loading...") { Tag = "placeholder" });
      }

      // Add special folders
      AddSpecialFolder(thisPcNode, "Desktop", Environment.SpecialFolder.Desktop);
      AddSpecialFolder(thisPcNode, "Documents", Environment.SpecialFolder.MyDocuments);
      AddSpecialFolder(thisPcNode, "Downloads", Environment.SpecialFolder.UserProfile, "Downloads");

      thisPcNode.Expand();
    }
    catch (Exception ex)
    {
      MessageBox.Show($"Error loading folder tree: {ex.Message}", "Error",
          MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }

  private void AddSpecialFolder(TreeNode parent, string displayName, Environment.SpecialFolder specialFolder, string? subPath = null)
  {
    try
    {
      var path = Environment.GetFolderPath(specialFolder);
      if (!string.IsNullOrEmpty(subPath))
      {
        path = Path.Combine(path, subPath);
      }

      if (Directory.Exists(path))
      {
        var node = new TreeNode(displayName)
        {
          Tag = path,
          ImageKey = "folder",
          SelectedImageKey = "folder"
        };

        _pathToNodeMap[path] = node;
        parent.Nodes.Add(node);

        // Add placeholder for lazy loading
        if (HasSubdirectories(path))
        {
          node.Nodes.Add(new TreeNode("Loading...") { Tag = "placeholder" });
        }
      }
    }
    catch
    {
      // Ignore errors for special folders
    }
  }

  private bool HasSubdirectories(string path)
  {
    try
    {
      return Directory.GetDirectories(path).Any();
    }
    catch
    {
      return false;
    }
  }

  private void FolderTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
  {
    if (e.Node?.Tag is string path && path != "ThisPC")
    {
      LoadSubdirectories(e.Node, path);
    }
  }

  private void LoadSubdirectories(TreeNode parentNode, string path)
  {
    // Remove placeholder
    if (parentNode.Nodes.Count == 1 && parentNode.Nodes[0].Tag?.ToString() == "placeholder")
    {
      parentNode.Nodes.Clear();
    }

    try
    {
      var directories = Directory.GetDirectories(path);
      foreach (var dir in directories)
      {
        var dirInfo = new DirectoryInfo(dir);

        // Skip hidden and system directories
        if ((dirInfo.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
          continue;

        var node = new TreeNode(dirInfo.Name)
        {
          Tag = dir,
          ImageKey = "folder",
          SelectedImageKey = "folder"
        };

        _pathToNodeMap[dir] = node;
        parentNode.Nodes.Add(node);

        // Add placeholder if has subdirectories
        if (HasSubdirectories(dir))
        {
          node.Nodes.Add(new TreeNode("Loading...") { Tag = "placeholder" });
        }
      }
    }
    catch (UnauthorizedAccessException)
    {
      // Access denied - add a message node
      parentNode.Nodes.Add(new TreeNode("Access Denied") { Tag = "error" });
    }
    catch (Exception ex)
    {
      // Other error
      parentNode.Nodes.Add(new TreeNode($"Error: {ex.Message}") { Tag = "error" });
    }
  }

  private void FolderTreeView_AfterSelect(object sender, TreeViewEventArgs e)
  {
    if (e.Node?.Tag is string path && path != "ThisPC" && path != "placeholder" && path != "error")
    {
      NavigateToPath(path);
    }
  }

  private void NavigateToPath(string path)
  {
    if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
      return;

    if (!string.IsNullOrEmpty(_currentPath) && _currentPath != path)
    {
      _backHistory.Push(_currentPath);
      _forwardHistory.Clear();
    }

    _currentPath = path;
    _addressBar.Text = path;

    LoadDirectoryContents(path);
    UpdateNavigationButtons();

    // Select node in tree if exists
    if (_pathToNodeMap.TryGetValue(path, out var node))
    {
      _folderTreeView.SelectedNode = node;
    }

    _statusLabel.Text = $"Path: {path}";
  }

  private void LoadDirectoryContents(string path)
  {
    _fileListView.Items.Clear();

    try
    {
      var directoryInfo = new DirectoryInfo(path);

      // Load directories first
      var directories = directoryInfo.GetDirectories()
          .Where(d => (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
          .OrderBy(d => d.Name);

      foreach (var dir in directories)
      {
        var item = new ListViewItem(dir.Name)
        {
          Tag = dir.FullName,
          ImageKey = "folder"
        };
        item.SubItems.AddRange(
        [
          dir.LastWriteTime.ToString("g"),
          "File folder",
          ""
        ]);
        _fileListView.Items.Add(item);
      }

      // Load files
      var files = directoryInfo.GetFiles()
          .Where(f => (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
          .OrderBy(f => f.Name);

      foreach (var file in files)
      {
        string imageKey = file.FullName;
        if (!_listImageList.Images.ContainsKey(imageKey))
        {
          try
          {
            using var icon = Icon.ExtractAssociatedIcon(file.FullName);
            if (icon != null)
            {
              _listImageList.Images.Add(imageKey, icon.ToBitmap());
            }
            else
            {
              _listImageList.Images.Add(imageKey, Resources.FileColorIcon);
            }
          }
          catch
          {
            _listImageList.Images.Add(imageKey, Resources.FileColorIcon);
          }
        }
        var item = new ListViewItem(file.Name)
        {
          Tag = file.FullName,
          ImageKey = imageKey
        };
        item.SubItems.AddRange(
        [
                    file.LastWriteTime.ToString("g"),
                    GetFileTypeDescription(file.Extension),
                    FormatFileSize(file.Length)
                ]);
        _fileListView.Items.Add(item);
      }

      _itemCountLabel.Text = $"{directories.Count()} folders, {files.Count()} files";
    }
    catch (UnauthorizedAccessException)
    {
      _statusLabel.Text = "Access denied to this location";
      _itemCountLabel.Text = "";
    }
    catch (Exception ex)
    {
      _statusLabel.Text = $"Error loading directory: {ex.Message}";
      _itemCountLabel.Text = "";
    }
  }

  private string GetFileTypeDescription(string extension)
  {
    if (string.IsNullOrEmpty(extension))
      return "File";

    return extension.ToUpper() switch
    {
      ".TXT" => "Text Document",
      ".PDF" => "PDF Document",
      ".DOC" or ".DOCX" => "Word Document",
      ".XLS" or ".XLSX" => "Excel Spreadsheet",
      ".JPG" or ".JPEG" or ".PNG" or ".GIF" or ".BMP" => "Image",
      ".MP3" or ".WAV" or ".WMA" => "Audio File",
      ".MP4" or ".AVI" or ".MOV" or ".WMV" => "Video File",
      ".ZIP" or ".RAR" or ".7Z" => "Archive",
      ".EXE" => "Application",
      ".DLL" => "Dynamic Link Library",
      _ => $"{extension.ToUpper().TrimStart('.')} File"
    };
  }

  private string FormatFileSize(long bytes)
  {
    string[] sizes = ["B", "KB", "MB", "GB", "TB"];
    double len = bytes;
    int order = 0;
    while (len >= 1024 && order < sizes.Length - 1)
    {
      order++;
      len /= 1024;
    }
    return $"{len:0.##} {sizes[order]}";
  }

  private void FileListView_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
  {
    var selectedCount = _fileListView.SelectedItems.Count;
    if (selectedCount == 0)
    {
      _statusLabel.Text = $"Path: {_currentPath}";
    }
    else if (selectedCount == 1)
    {
      var item = _fileListView.SelectedItems[0];
      _statusLabel.Text = Path.GetFileName(item.Tag?.ToString() ?? "");
    }
    else
    {
      _statusLabel.Text = $"{selectedCount} items selected";
    }
  }

  private void FileListView_DoubleClick(object sender, EventArgs e)
  {
    if (_fileListView.SelectedItems.Count == 1)
    {
      var item = _fileListView.SelectedItems[0];
      var path = item.Tag?.ToString();

      if (string.IsNullOrEmpty(path))
        return;

      if (Directory.Exists(path))
      {
        NavigateToPath(path!);
      }
      else if (File.Exists(path))
      {
        try
        {
          Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
          MessageBox.Show($"Cannot open file: {ex.Message}", "Error",
              MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
      }
    }
  }

  private void FileListView_KeyDown(object sender, KeyEventArgs e)
  {
    if (e.KeyCode == Keys.Enter && _fileListView.SelectedItems.Count == 1)
    {
      FileListView_DoubleClick(sender, e);
    }
    else if (e.KeyCode == Keys.Back)
    {
      NavigateUp();
    }
  }

  private void BackButton_Click(object sender, EventArgs e)
  {
    if (_backHistory.Count > 0)
    {
      _forwardHistory.Push(_currentPath);
      var previousPath = _backHistory.Pop();
      NavigateToPathWithoutHistory(previousPath);
      UpdateNavigationButtons();
    }
  }

  private void ForwardButton_Click(object sender, EventArgs e)
  {
    if (_forwardHistory.Count > 0)
    {
      _backHistory.Push(_currentPath);
      var nextPath = _forwardHistory.Pop();
      NavigateToPathWithoutHistory(nextPath);
      UpdateNavigationButtons();
    }
  }

  private void UpButton_Click(object sender, EventArgs e)
  {
    NavigateUp();
  }

  private void NavigateUp()
  {
    if (!string.IsNullOrEmpty(_currentPath))
    {
      var parent = Directory.GetParent(_currentPath);
      if (parent != null)
      {
        NavigateToPath(parent.FullName);
      }
    }
  }

  private void NavigateToPathWithoutHistory(string path)
  {
    _currentPath = path;
    _addressBar.Text = path;
    LoadDirectoryContents(path);

    if (_pathToNodeMap.TryGetValue(path, out var node))
    {
      _folderTreeView.SelectedNode = node;
    }

    _statusLabel.Text = $"Path: {path}";
  }

  private void UpdateNavigationButtons()
  {
    _backButton.Enabled = _backHistory.Count > 0;
    _forwardButton.Enabled = _forwardHistory.Count > 0;
    _upButton.Enabled = !string.IsNullOrEmpty(_currentPath) && Directory.GetParent(_currentPath) != null;
  }

  private void AddressBar_KeyDown(object sender, KeyEventArgs e)
  {
    if (e.KeyCode == Keys.Enter)
    {
      var path = _addressBar.Text.Trim();
      if (Directory.Exists(path))
      {
        NavigateToPath(path);
      }
      else
      {
        MessageBox.Show("The specified path does not exist.", "Invalid Path",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        _addressBar.Text = _currentPath;
      }
    }
  }

  private void RefreshButton_Click(object sender, EventArgs e)
  {
    if (!string.IsNullOrEmpty(_currentPath))
    {
      LoadDirectoryContents(_currentPath);
    }
  }

  private void ViewMenuItem_Click(object sender, EventArgs e)
  {
    if (sender is ToolStripMenuItem menuItem && menuItem.Tag is View view)
    {
      _fileListView.View = view;

      // Update checked state
      foreach (ToolStripMenuItem item in _viewButton.DropDownItems)
      {
        item.Checked = item == menuItem;
      }
    }
  }


  protected override void OnResize(EventArgs e)
  {
    base.OnResize(e);
    if (_closeButton is null)
    {
      return;
    }

    _closeButton.Location = new Point(Width - _closeButton.Width - 15, Height - _closeButton.Height - 35);
    if (_mainSplitContainer is null)
    {
      return;
    }

    _mainSplitContainer.Height = Height - _toolStrip.Height - _statusStrip.Height - _closeButton.Height - 15;
  }
}