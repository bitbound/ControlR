namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerStats;

public record ServerStatsDto(
  int TotalTenants,
  int OnlineAgents,
  int TotalAgents,
  int OnlineUsers,
  int TotalUsers);
