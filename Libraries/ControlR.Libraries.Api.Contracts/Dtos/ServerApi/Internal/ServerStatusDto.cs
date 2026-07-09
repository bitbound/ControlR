namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

[MessagePackObject(keyAsPropertyName: true)]
public record ServerStatsDto(
  int TotalTenants,
  int OnlineAgents,
  int TotalAgents,
  int OnlineUsers,
  int TotalUsers);