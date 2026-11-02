using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public class RemoteControlDtoWrapper
{
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public required RemoteControlDtoType DtoType { get; init; }

  public required byte[] Payload { get; init; }
  public required long SendTimestamp { get; init; }

  public static RemoteControlDtoWrapper Create<T>(T dto, RemoteControlDtoType dtoType)
  {
    return new RemoteControlDtoWrapper()
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
