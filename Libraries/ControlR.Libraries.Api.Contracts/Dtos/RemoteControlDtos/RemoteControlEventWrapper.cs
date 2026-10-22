using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public class RemoteControlEventWrapper
{
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public required RemoteControlEventDtoType DtoType { get; init; }

  public required byte[] Payload { get; init; }
  public required long SendTimestamp { get; init; }

  public static RemoteControlEventWrapper Create<T>(T dto, RemoteControlEventDtoType dtoType)
  {
    return new RemoteControlEventWrapper()
    {
      DtoType = dtoType,
      Payload = MessagePackSerializer.Serialize(dto),
      SendTimestamp = TimeProvider.System.GetTimestamp()
    };
  }

  public T GetPayload<T>()
  {
    return MessagePackSerializer.Deserialize<T>(Payload);
  }
}
