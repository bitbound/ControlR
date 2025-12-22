namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record VideoStreamPacketDto(
  [property: Key(0)]
  byte[] PacketData,
  [property: Key(1)]
  long Timestamp);