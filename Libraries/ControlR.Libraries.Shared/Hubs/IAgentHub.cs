using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;

namespace ControlR.Libraries.Shared.Hubs;
public interface IAgentHub
{
  IAsyncEnumerable<byte[]> GetFileUploadStream(FileUploadHubDto dto);
  Task<bool> SendChatResponse(ChatResponseHubDto responseDto);
  Task SendDesktopClientDownloadProgress(DesktopClientDownloadProgressDto progressDto);
  Task SendDesktopPreviewStream(Guid streamId, IAsyncEnumerable<byte[]> jpegChunks);
  Task SendFileDownloadStream(Guid streamId, IAsyncEnumerable<byte[]> fileChunks);
  Task SendDirectoryContentsStream(Guid streamId, bool directoryExists, IAsyncEnumerable<FileSystemEntryDto[]> entryChunks);
  Task SendSubdirectoriesStream(Guid streamId, IAsyncEnumerable<FileSystemEntryDto[]> subdirectoryChunks);
  Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
  Task<Result<DeviceDto>> UpdateDevice(DeviceDto deviceDto);
}
