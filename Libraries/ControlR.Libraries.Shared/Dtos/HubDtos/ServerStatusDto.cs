namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ServerStatsDto(
  int TotalTenants,
  int OnlineAgents,
  int TotalAgents,
  int OnlineUsers,
  int TotalUsers);