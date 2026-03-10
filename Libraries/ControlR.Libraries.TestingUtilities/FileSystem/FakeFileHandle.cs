namespace ControlR.Libraries.TestingUtilities.FileSystem;

internal sealed class FakeFileHandle(FileAccess fileAccess, FileShare fileShare)
{
  public FileAccess FileAccess { get; } = fileAccess;

  public FileShare FileShare { get; } = fileShare;
}
