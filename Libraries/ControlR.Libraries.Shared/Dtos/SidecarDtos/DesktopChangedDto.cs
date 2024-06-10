namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;

public record DesktopChangedDto(string DesktopName) : SidecarDtoBase(SidecarDtoType.DesktopChanged);
