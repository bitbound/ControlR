using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

namespace ControlR.DesktopClient.Common.Messages;

public sealed record SendRemoteControlDtoToViewerMessage(RemoteControlEventWrapper Wrapper);
