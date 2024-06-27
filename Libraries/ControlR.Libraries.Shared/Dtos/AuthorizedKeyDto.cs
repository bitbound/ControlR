namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record AuthorizedKeyDto(
    [property: MsgPackKey] string Label, 
    [property: MsgPackKey] string PublicKey);
