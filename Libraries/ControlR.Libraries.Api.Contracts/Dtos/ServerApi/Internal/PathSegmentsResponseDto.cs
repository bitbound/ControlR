namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

[MessagePackObject(keyAsPropertyName: true)]
public record PathSegmentsResponseDto
{
  public string? ErrorMessage { get; init; }
  public required bool PathExists { get; init; }
  public required IReadOnlyList<string> PathSegments { get; init; }
  public string PathSeparator { get; init; } = Path.DirectorySeparatorChar.ToString();
  public required bool Success { get; init; }
}
