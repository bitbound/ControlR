namespace ControlR.Web.Client.Components.FileSystem;

public class FileSystemEntryViewModel
{
  public required bool CanRead { get; set; }
  public required bool CanWrite { get; set; }

  public string FormattedSize => IsDirectory ? string.Empty : FormatBytes(Size);
  public required string FullPath { get; set; }
  
  public string Icon => IsDirectory ? Icons.Material.Filled.Folder : GetFileIcon();
  public required bool IsDirectory { get; set; }
  public required bool IsHidden { get; set; }
  public required DateTimeOffset LastModified { get; set; }
  public required string Name { get; set; }
  public required long Size { get; set; }

  private static string FormatBytes(long bytes)
  {
    string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
    var counter = 0;
    var number = (decimal)bytes;
    while (Math.Round(number / 1024) >= 1)
    {
      number /= 1024;
      counter++;
    }
    return $"{number:n1} {suffixes[counter]}";
  }

  private string GetFileIcon()
  {
    var extension = Path.GetExtension(Name).ToLowerInvariant();
    return extension switch
    {
      ".txt" or ".md" or ".log" => Icons.Material.Filled.Description,
      ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => Icons.Material.Filled.Image,
      ".mp4" or ".avi" or ".mov" or ".wmv" => Icons.Material.Filled.VideoFile,
      ".mp3" or ".wav" or ".flac" or ".aac" => Icons.Material.Filled.AudioFile,
      ".pdf" => Icons.Material.Filled.PictureAsPdf,
      ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => Icons.Material.Filled.Archive,
      ".exe" or ".msi" => Icons.Material.Filled.Launch,
      _ => Icons.Material.Filled.InsertDriveFile
    };
  }
}
