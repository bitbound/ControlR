using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public class UnsignedPayloadDto
{
    [MsgPackKey]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required DtoType DtoType { get; init; }

    [MsgPackKey]
    public required byte[] Payload { get; init; }

    public static UnsignedPayloadDto Create<T>(T dto, DtoType dtoType)
    {
        return new UnsignedPayloadDto()
        {
            DtoType = dtoType,
            Payload = MessagePackSerializer.Serialize(dto)
        };
    }

    public T GetPayload<T>()
    {
        return MessagePackSerializer.Deserialize<T>(Payload);
    }
}
