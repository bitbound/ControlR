namespace ControlR.Tests.TestingUtilities;

public static class IoHelper
{
  public static string GetSolutionDir(string currentDir)
  {
    var dirInfo = new DirectoryInfo(currentDir);
    if (!dirInfo.Exists)
    {
      throw new DirectoryNotFoundException($"Directory '{currentDir}' does not exist.");
    }

    if (dirInfo.GetFiles().Any(x => x.Name == "ControlR.sln"))
    {
      return dirInfo.FullName;
    }

    if (dirInfo.Parent is not null)
    {
      return GetSolutionDir(dirInfo.Parent.FullName);
    }

    throw new DirectoryNotFoundException($"Directory '{currentDir}' does not contain a solution file.");
  }
}