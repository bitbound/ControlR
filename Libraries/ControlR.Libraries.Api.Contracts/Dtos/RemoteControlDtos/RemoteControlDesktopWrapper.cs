using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public class RemoteControlDesktopWrapper
{
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public required RemoteControlDesktopDtoType DtoType { get; init; }

  public required byte[] Payload { get; init; }
  public required long SendTimestamp { get; init; }

  public static RemoteControlDesktopWrapper Create<T>(T dto, RemoteControlDesktopDtoType dtoType)
  {
    return new RemoteControlDesktopWrapper()
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
