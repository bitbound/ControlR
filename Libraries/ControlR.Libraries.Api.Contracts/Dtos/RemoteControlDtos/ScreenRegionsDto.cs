namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject]
public record ScreenRegionsDto(
  [property: Key(0)]
  ScreenRegionDto[] Regions);