namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

public record GetPathSegmentsHubDto
{
  public required string TargetPath { get; init; }
}
