namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record ServerStatsDto(
    [property: MsgPackKey] int AgentCount,
    [property: MsgPackKey] int ViewerCount);