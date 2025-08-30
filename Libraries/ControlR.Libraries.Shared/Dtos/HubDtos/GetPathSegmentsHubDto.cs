namespace ControlR.Libraries.Shared.Dtos.HubDtos;

public record GetPathSegmentsHubDto
{
  public required string TargetPath { get; init; }
}
