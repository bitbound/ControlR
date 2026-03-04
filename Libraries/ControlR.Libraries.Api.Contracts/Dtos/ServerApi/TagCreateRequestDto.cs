using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record TagCreateRequestDto(string Name, TagType Type);