using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public class RemoteControlCommandWrapper
{
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public required RemoteControlCommandDtoType DtoType { get; init; }

  public required byte[] Payload { get; init; }
  public required long SendTimestamp { get; init; }

  public static RemoteControlCommandWrapper Create<T>(T dto, RemoteControlCommandDtoType dtoType)
  {
    return new RemoteControlCommandWrapper()
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
