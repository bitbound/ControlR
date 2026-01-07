using System.Threading.Channels;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Dtos.ServerApi;

namespace ControlR.Libraries.Shared.Hubs;

public interface IAgentHub
{
  ChannelReader<byte[]> GetFileStreamFromViewer(FileUploadHubDto dto);
  Task<bool> SendChatResponse(ChatResponseHubDto responseDto);
  Task SendDesktopPreviewStream(Guid streamId, ChannelReader<byte[]> jpegChunks);
  Task SendDirectoryContentsStream(Guid streamId, bool directoryExists, ChannelReader<FileSystemEntryDto[]> entryChunks);
  Task<Result> SendFileContentStream(Guid streamId, ChannelReader<byte[]> fileChunks);
  Task SendSubdirectoriesStream(Guid streamId, ChannelReader<FileSystemEntryDto[]> subdirectoryChunks);
  Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
  Task<Result<DeviceResponseDto>> UpdateDevice(DeviceUpdateRequestDto agentDto);
}
