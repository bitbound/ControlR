using System.Threading.Channels;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.Libraries.Api.Contracts.Hubs;

public interface IAgentHub
{
  ChannelReader<byte[]> GetFileStreamFromViewer(FileUploadHubDto dto);
  Task<bool> SendChatResponse(ChatResponseHubDto responseDto);
  Task SendDesktopPreviewStream(Guid streamId, ChannelReader<byte[]> jpegChunks);
  Task SendDirectoryContentsStream(Guid streamId, bool directoryExists, ChannelReader<FileSystemEntryDto[]> entryChunks);
  Task<HubResult> SendFileContentStream(Guid streamId, ChannelReader<byte[]> fileChunks);
  Task SendSubdirectoriesStream(Guid streamId, ChannelReader<FileSystemEntryDto[]> subdirectoryChunks);
  Task SendTerminalOutputToViewer(string viewerConnectionId, TerminalOutputDto outputDto);
  Task<HubResult<DeviceResponseDto>> UpdateDevice(DeviceUpdateRequestDto agentDto);
}
