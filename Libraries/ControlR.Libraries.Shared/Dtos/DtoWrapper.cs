using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject(keyAsPropertyName: true)]
public class DtoWrapper
{
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public required DtoType DtoType { get; init; }

  public required byte[] Payload { get; init; }
  public required long SendTimestamp { get; init; }

  public static DtoWrapper Create<T>(T dto, DtoType dtoType)
  {
    return new DtoWrapper()
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