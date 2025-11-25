using System.Buffers;
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record VideoStreamPacketDto(byte[] PacketData, long Timestamp);