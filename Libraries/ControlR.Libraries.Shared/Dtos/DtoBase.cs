namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public class DtoBase()
{
    [MsgPackKey]
    public Guid InstanceId { get; init; } = Guid.NewGuid();
}

[MessagePackObject]
public record DtoRecordBase()
{
    [MsgPackKey]
    public Guid InstanceId { get; init; } = Guid.NewGuid();
}