using ControlR.Libraries.Shared.Dtos.StreamerDtos;

namespace ControlR.Libraries.Shared.Hubs;
public interface IStreamerHub
{
    Task SendStreamerInitDataToViewer(string viewerConnectionId, StreamerInitDataDto streamerInit);
    Task SendClipboardChangeToViewer(string viewerConnectionId, ClipboardChangeDto clipboardChangeDto);
}