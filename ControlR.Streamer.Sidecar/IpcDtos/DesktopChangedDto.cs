namespace ControlR.Streamer.Sidecar.IpcDtos;

public record DesktopChangedDto(string DesktopName) : SidecarDtoBase(SidecarDtoType.DesktopChanged);
