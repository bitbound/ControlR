using ControlR.Libraries.Shared.Dtos.ServerApi;

namespace ControlR.Agent.Common.Services.FileManager;

public record DirectoryContentsResult(
  FileSystemEntryDto[] Entries,
  bool DirectoryExists);
