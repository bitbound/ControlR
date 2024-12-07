namespace ControlR.Libraries.Shared.Dtos.ServerApi;
public record class TagRenameRequestDto(Guid TagId, string NewTagName);