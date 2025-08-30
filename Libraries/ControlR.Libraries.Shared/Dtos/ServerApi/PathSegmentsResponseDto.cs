namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record PathSegmentsResponseDto
{
  public string? ErrorMessage { get; init; }
  public required bool PathExists { get; init; }
  public required string[] PathSegments { get; init; }
  public string PathSeparator { get; init; } = Path.DirectorySeparatorChar.ToString();
  public required bool Success { get; init; }
}
