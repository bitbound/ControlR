namespace ControlR.Shared.Dtos.SidecarDtos;
public record WheelScrollDto(double X, double Y, double ScrollY) : SidecarDtoBase(SidecarDtoType.WheelScroll);