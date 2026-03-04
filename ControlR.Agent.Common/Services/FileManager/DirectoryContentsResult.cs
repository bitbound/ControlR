using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.Agent.Common.Services.FileManager;

public record DirectoryContentsResult(
  FileSystemEntryDto[] Entries,
  bool DirectoryExists);
