namespace ControlR.Shared.Dtos;

[MessagePackObject]
public record ServerStatsDto(
    [property: MsgPackKey] int AgentCount,
    [property: MsgPackKey] int ViewerCount,
    [property: MsgPackKey] int StreamCount);