namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record ServerStatsDto(
    [property: Key(0)] int AgentCount,
    [property: Key(1)] int ViewerCount);