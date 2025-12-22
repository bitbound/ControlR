namespace ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

[MessagePackObject]
public record VideoStreamPacketDto(
  [property: Key(0)]
  byte[] PacketData,
  [property: Key(1)]
  long Timestamp);