using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public class RemoteControlViewerWrapper
{
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public required RemoteControlViewerDtoType DtoType { get; init; }

  public required byte[] Payload { get; init; }
  public required long SendTimestamp { get; init; }

  public static RemoteControlViewerWrapper Create<T>(T dto, RemoteControlViewerDtoType dtoType)
  {
    return new RemoteControlViewerWrapper()
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
