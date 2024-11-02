using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record TagCreateRequestDto(string Name, TagType Type);