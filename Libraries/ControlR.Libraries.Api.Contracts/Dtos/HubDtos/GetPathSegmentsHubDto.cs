namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record GetPathSegmentsHubDto
{
  public required string TargetPath { get; init; }
}
