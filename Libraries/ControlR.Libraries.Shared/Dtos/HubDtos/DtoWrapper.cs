﻿using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.HubDtos;


[MessagePackObject]
public class DtoWrapper
{
    [MsgPackKey]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required DtoType DtoType { get; init; }

    [MsgPackKey]
    public required byte[] Payload { get; init; }

    public static DtoWrapper Create<T>(T dto, DtoType dtoType)
    {
        return new DtoWrapper()
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