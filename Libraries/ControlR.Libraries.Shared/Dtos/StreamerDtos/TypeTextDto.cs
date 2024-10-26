namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record TypeTextDto([property: MsgPackKey] string Text);