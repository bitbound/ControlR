namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record ServerStatsDto(
    [property: Key(0)] int TotalTenants,
    [property: Key(1)] int OnlineAgents,
    [property: Key(2)] int TotalAgents,
    [property: Key(3)] int OnlineUsers,
    [property: Key(4)] int TotalUsers);