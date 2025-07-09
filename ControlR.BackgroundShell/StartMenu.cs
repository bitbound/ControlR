using System.Diagnostics;
using ControlR.BackgroundShell.Properties;

namespace ControlR.BackgroundShell;
public partial class StartMenu : Form
{
  private readonly string[] _startMenuPaths =
  [
    @"C:\ProgramData\Microsoft\Windows\Start Menu\",
    @"C:\Users\Default\AppData\Roaming\Microsoft\Windows\Start Menu\",
    @"C:\Windows\System32\catroot2\{F750E6C3-38EE-11D1-85E5-00C04FC295EE}\Microsoft\Windows\Start Menu\", // For SYSTEM user
    @"C:\Windows\ServiceProfiles\NetworkService\AppData\Roaming\Microsoft\Windows\Start Menu\",
    @"C:\Windows\ServiceProfiles\LocalService\AppData\Roaming\Microsoft\Windows\Start Menu\"
  ];

  public StartMenu()
  {
    InitializeComponent();
    PopulateStartMenu();
  }

  protected override void SetVisibleCore(bool value)
  {
    if (value && WindowState == FormWindowState.Minimized)
    {
      WindowState = FormWindowState.Normal;
    }
    base.SetVisibleCore(value);
  }

  protected override void WndProc(ref Message m)
  {
    const int WM_NCLBUTTONDOWN = 0x00A1;
    const int WM_SYSCOMMAND = 0x0112;
    const int SC_MOVE = 0xF010;
    const int SC_SIZE = 0xF000;

    // Prevent dragging by blocking WM_NCLBUTTONDOWN on the title bar
    if (m.Msg == WM_NCLBUTTONDOWN)
    {
      return;
    }

    // Prevent moving and resizing via system commands
    if (m.Msg == WM_SYSCOMMAND)
    {
      int command = m.WParam.ToInt32() & 0xFFF0;
      if (command is SC_MOVE or SC_SIZE)
      {
        return;
      }
    }

    base.WndProc(ref m);
  }

  private string GetFileIcon(string filePath)
  {
    // Use the file path as the key to avoid duplicates
    if (!_treeView.ImageList.Images.ContainsKey(filePath))
    {
      try
      {
        using var icon = Icon.ExtractAssociatedIcon(filePath);
        if (icon != null)
        {
          _treeView.ImageList.Images.Add(filePath, icon.ToBitmap());
          return filePath;
        }
      }
      catch
      {
        // Fall through to default
      }

      // Fallback to default file icon
      return "file";
    }

    return filePath;
  }

  private void LoadNodeChildren(TreeNode parentNode)
  {
    if (parentNode.Tag is not FolderInfo folderInfo)
      return;

    // Check if already loaded
    if (folderInfo.IsLoaded)
      return;

    // Remove placeholder
    parentNode.Nodes.Clear();

    // Collect paths to scan (main path + any merge paths)
    var pathsToScan = new List<string> { folderInfo.Path };
    if (folderInfo.MergePaths != null)
    {
      pathsToScan.AddRange(folderInfo.MergePaths);
    }

    // Scan directories and files from all paths
    var scanResult = DirectoryScanner.ScanDirectories(pathsToScan, ScanOptions.AllFiles);
    
    // Add subdirectories to the tree
    foreach (var kvp in scanResult.Directories.OrderBy(x => x.Key))
    {
      var dirNode = CreateFolderNode(kvp.Key, kvp.Value);
      parentNode.Nodes.Add(dirNode);
    }

    // Add files to the tree
    foreach (var kvp in scanResult.Files.OrderBy(x => x.Key))
    {
      var fileNode = CreateFileNode(kvp.Key, kvp.Value);
      parentNode.Nodes.Add(fileNode);
    }

    // Mark as loaded
    folderInfo.IsLoaded = true;

    // If no content was found, add a message
    if (parentNode.Nodes.Count == 0)
    {
      parentNode.Nodes.Add(new TreeNode("(Empty)") { Tag = "empty" });
    }
  }

  private void PopulateStartMenu()
  {
    _treeView.Nodes.Clear();
    _treeView.ImageList = new ImageList { ImageSize = new Size(16, 16) };
    
    // Add default icons
    _treeView.ImageList.Images.Add("folder", Resources.FolderIcon);
    _treeView.ImageList.Images.Add("file", Resources.FileColorIcon);

    var rootItems = new Dictionary<string, TreeNode>();

    foreach (var basePath in _startMenuPaths)
    {
      if (Directory.Exists(basePath))
      {
        try
        {
          ScanDirectoryForRootItems(basePath, rootItems);
        }
        catch (Exception ex)
        {
          // Log error but continue with other paths
          System.Diagnostics.Debug.WriteLine($"Error scanning {basePath}: {ex.Message}");
        }
      }
    }
    
    // Add all root items to the tree
    var sortedItems = rootItems.Values
      .OrderBy(x => x.Tag is FolderInfo ? 0 : 1)
      .ThenBy(n => n.Text)
      .ToArray();
    _treeView.Nodes.AddRange(sortedItems);
  }

  private void ScanDirectoryForRootItems(string basePath, Dictionary<string, TreeNode> rootItems)
  {
    try
    {
      // Scan the base directory, excluding Programs folder
      var scanResult = DirectoryScanner.ScanDirectory(basePath, ScanOptions.AllFiles, ["Programs"]);
      
      // Add directories and files from the base path
      MergeScannedItems(scanResult, rootItems);

      // Now flatten the Programs folder contents into the root
      var programsPath = Path.Combine(basePath, "Programs");
      if (Directory.Exists(programsPath))
      {
        var programsScanResult = DirectoryScanner.ScanDirectory(programsPath, ScanOptions.AllFiles);
        MergeScannedItems(programsScanResult, rootItems);
      }
    }
    catch (UnauthorizedAccessException)
    {
      // Skip directories we can't access
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine($"Error processing directory {basePath}: {ex.Message}");
    }
  }

  private void MergeScannedItems(DirectoryScanner.ScanResult scanResult, Dictionary<string, TreeNode> rootItems)
  {
    // Merge directories
    foreach (var kvp in scanResult.Directories)
    {
      MergeOrCreateFolderNode(kvp.Key, kvp.Value, rootItems);
    }

    // Merge files
    foreach (var kvp in scanResult.Files)
    {
      if (!rootItems.ContainsKey(kvp.Key))
      {
        rootItems[kvp.Key] = CreateFileNode(kvp.Key, kvp.Value);
      }
    }
  }

  private void MergeOrCreateFolderNode(string nodeName, string folderPath, Dictionary<string, TreeNode> rootItems)
  {
    if (!rootItems.ContainsKey(nodeName))
    {
      rootItems[nodeName] = CreateFolderNode(nodeName, folderPath);
    }
    else
    {
      // Merge content - update the existing node to include this path
      var existingNode = rootItems[nodeName];
      if (existingNode.Tag is FolderInfo folderInfo)
      {
        // Store multiple paths for merging
        folderInfo.MergePaths ??= [];
        if (!folderInfo.MergePaths.Contains(folderPath))
        {
          folderInfo.MergePaths.Add(folderPath);
        }
      }
      
      // Ensure it has a placeholder for expand icon if it doesn't already
      if (existingNode.Nodes.Count == 0)
      {
        existingNode.Nodes.Add(new TreeNode("Loading...") { Tag = "placeholder" });
      }
    }
  }

  private TreeNode CreateFolderNode(string displayName, string folderPath)
  {
    var dirNode = new TreeNode(displayName) 
    { 
      ImageKey = "folder", 
      SelectedImageKey = "folder",
      Tag = new FolderInfo { Path = folderPath, IsLoaded = false }
    };
    
    // Always add a placeholder to show expand icon - we'll load on demand
    dirNode.Nodes.Add(new TreeNode("Loading...") { Tag = "placeholder" });
   
    return dirNode;
  }

  private TreeNode CreateFileNode(string displayName, string filePath)
  {
    var imageKey = GetFileIcon(filePath);
    return new TreeNode(displayName)
    {
      ImageKey = imageKey,
      SelectedImageKey = imageKey,
      Tag = filePath
    };
  }

  private void StartMenu_Deactivate(object sender, EventArgs e)
  {
    // Close the start menu when it loses focus (like classic Windows behavior)
    Close();
  }

  private void TreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
  {
    // Check if this node has placeholder children and needs to be loaded
    if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Tag?.ToString() == "placeholder")
    {
      LoadNodeChildren(e.Node);
    }
  }

  private void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
  {
    if (e.Node.Tag is string tag)
    {
      if (tag is "placeholder" or "error")
      {
        // Ignore clicks on placeholder and error nodes
        return;
      }
      else
      {
        // This is a file - launch it
        try
        {
          var startInfo = new ProcessStartInfo
          {
            FileName = tag,
            UseShellExecute = true
          };
          Process.Start(startInfo);
          Close();
        }
        catch (Exception ex)
        {
          MessageBox.Show($"Failed to launch: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
      }
    }
    else if (e.Node.Tag is FolderInfo)
    {
      // This is a folder - expand/collapse
      if (e.Node.IsExpanded)
        e.Node.Collapse();
      else
        e.Node.Expand();
    }
  }

  // Helper class to track folder information
  private class FolderInfo
  {
    public bool IsLoaded { get; set; } = false;
    public List<string>? MergePaths { get; set; }
    public string Path { get; set; } = "";
  }

  // Reusable directory scanning logic
  private static class DirectoryScanner
  {
    public static ScanResult ScanDirectory(string directoryPath, ScanOptions options, string[]? excludeDirectories = null)
    {
      var paths = new List<string> { directoryPath };
      return ScanDirectories(paths, options, excludeDirectories);
    }

    public static ScanResult ScanDirectories(IEnumerable<string> directoryPaths, ScanOptions options, string[]? excludeDirectories = null)
    {
      var allSubDirs = new Dictionary<string, string>(); // name -> full path
      var allFiles = new Dictionary<string, string>(); // display name -> full path

      foreach (var directoryPath in directoryPaths)
      {
        if (!Directory.Exists(directoryPath))
          continue;

        try
        {
          var dirInfo = new DirectoryInfo(directoryPath);

          // Collect subdirectories
          foreach (var subDir in dirInfo.GetDirectories())
          {
            if (ShouldSkipItem(subDir.Name, subDir.Attributes))
              continue;

            // Check if this directory should be excluded
            if (excludeDirectories?.Contains(subDir.Name, StringComparer.OrdinalIgnoreCase) == true)
              continue;

            // Use the first occurrence of each directory name
            if (!allSubDirs.ContainsKey(subDir.Name))
            {
              allSubDirs[subDir.Name] = subDir.FullName;
            }
          }

          // Collect files based on options
          if (options.HasFlag(ScanOptions.AllFiles) || options.HasFlag(ScanOptions.ExecutableFiles))
          {
            foreach (var file in dirInfo.GetFiles())
            {
              if (ShouldSkipItem(file.Name, file.Attributes))
                continue;

              // Filter files based on options
              if (options.HasFlag(ScanOptions.ExecutableFiles) && !IsExecutableFile(file.Extension))
                continue;

              var displayName = Path.GetFileNameWithoutExtension(file.Name);

              // Use the first occurrence of each file name
              if (!allFiles.ContainsKey(displayName))
              {
                allFiles[displayName] = file.FullName;
              }
            }
          }
        }
        catch (UnauthorizedAccessException)
        {
          // Skip directories we can't access
        }
        catch (Exception ex)
        {
          System.Diagnostics.Debug.WriteLine($"Error processing directory {directoryPath}: {ex.Message}");
        }
      }

      return new ScanResult { Directories = allSubDirs, Files = allFiles };
    }

    private static bool ShouldSkipItem(string name, FileAttributes attributes)
    {
      return name.StartsWith(".") || (attributes & FileAttributes.Hidden) != 0;
    }

    private static bool IsExecutableFile(string extension)
    {
      var executableExtensions = new[] { ".lnk", ".exe", ".bat", ".cmd" };
      return executableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public class ScanResult
    {
      public Dictionary<string, string> Directories { get; set; } = [];
      public Dictionary<string, string> Files { get; set; } = [];
    }
  }

  [Flags]
  private enum ScanOptions
  {
    None = 0,
    ExecutableFiles = 1,
    AllFiles = 2
  }
}
