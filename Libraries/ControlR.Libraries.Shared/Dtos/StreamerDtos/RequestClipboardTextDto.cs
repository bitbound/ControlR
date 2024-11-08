using ControlR.Libraries.Shared.Dtos.HubDtos;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record RequestClipboardTextDto() : ParameterlessDtoBase(DtoType.RequestClipboardText);
