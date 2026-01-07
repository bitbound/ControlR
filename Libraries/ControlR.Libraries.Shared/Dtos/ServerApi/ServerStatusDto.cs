namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject(keyAsPropertyName: true)]
public record ServerStatsDto(
  int TotalTenants,
  int OnlineAgents,
  int TotalAgents,
  int OnlineUsers,
  int TotalUsers);