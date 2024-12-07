using System.Text.Json.Serialization;
using ControlR.Libraries.Shared.Dtos.HubDtos;

namespace ControlR.Libraries.Shared.Dtos;


[MessagePackObject]
public class DtoWrapper
{
  [Key(nameof(DtoType))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public required DtoType DtoType { get; init; }

  [Key(nameof(Payload))]
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