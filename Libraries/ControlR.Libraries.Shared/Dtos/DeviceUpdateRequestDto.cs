namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record DeviceUpdateRequestDto(
    [property: MsgPackKey] string PublicKeyLabel) : DtoRecordBase;
