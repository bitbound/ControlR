namespace ControlR.Libraries.Shared.Helpers;

public static class IoHelper
{
  public static Result<string> GetSolutionDir(string currentDir)
  {
    var dirInfo = new DirectoryInfo(currentDir);
    if (!dirInfo.Exists)
    {
      throw new DirectoryNotFoundException($"Directory '{currentDir}' does not exist.");
    }

    if (dirInfo.GetFiles().Any(x => x.Name == "ControlR.slnx"))
    {
      return Result.Ok(dirInfo.FullName);
    }

    if (dirInfo.Parent is not null)
    {
      return GetSolutionDir(dirInfo.Parent.FullName);
    }

    return Result.Fail<string>("Solution directory not found.");
  }
}