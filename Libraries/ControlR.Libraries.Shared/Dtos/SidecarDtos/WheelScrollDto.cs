namespace ControlR.Libraries.Shared.Dtos.SidecarDtos;
public record WheelScrollDto(double X, double Y, double? ScrollY, double? ScrollX) : SidecarDtoBase(SidecarDtoType.WheelScroll);