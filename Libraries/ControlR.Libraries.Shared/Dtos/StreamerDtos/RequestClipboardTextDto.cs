using ControlR.Libraries.Shared.Dtos.HubDtos;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record RequestClipboardTextDto();
