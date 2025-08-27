using ControlR.Libraries.Shared.Dtos.ServerApi;

namespace ControlR.Agent.Common.Interfaces;

public interface IFileManager
{
  Task<FileSystemEntryDto[]> GetRootDrives();
  Task<FileSystemEntryDto[]> GetSubdirectories(string directoryPath);
  Task<FileSystemEntryDto[]> GetDirectoryContents(string directoryPath);
}
