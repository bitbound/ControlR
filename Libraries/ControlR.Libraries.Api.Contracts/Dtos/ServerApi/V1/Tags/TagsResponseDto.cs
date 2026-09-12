namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Tags;

public class TagsResponseDto
{
  public IReadOnlyList<TagResponseDto> Items { get; set; } = [];
}
