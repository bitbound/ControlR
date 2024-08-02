namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public class IdentityDto : DtoBase
{
    [MsgPackKey]
    public required string Username { get; init; }
}
