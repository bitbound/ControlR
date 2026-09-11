namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Tags;

public record TagResponseDto(
  Guid Id,
  string Name,
  TagType Type,
  IReadOnlyList<Guid> DeviceIds)
{
  public override string ToString() => Name;
}
