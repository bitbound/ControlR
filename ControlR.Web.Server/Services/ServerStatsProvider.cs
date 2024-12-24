using ControlR.Libraries.Shared.Dtos.HubDtos;

namespace ControlR.Web.Server.Services;

public interface IServerStatsProvider
{
  Task<Result<ServerStatsDto>> GetServerStats();
}

public class ServerStatsProvider(
  AppDb appDb,
  ILogger<ServerStatsProvider> logger) : IServerStatsProvider
{
  private readonly AppDb _appDb = appDb;

  public async Task<Result<ServerStatsDto>> GetServerStats()
  {
    try
    {
      var totalTenants = await _appDb.Tenants.CountAsync();

      var agents = await _appDb.Devices
        .AsNoTracking()
        .IgnoreQueryFilters()
        .Select(x => new { x.IsOnline })
        .ToListAsync();

      var totalAgents = agents.Count;
      var onlineAgents = agents.Count(x => x.IsOnline);

      var users = await _appDb.Users
        .AsNoTracking()
        .IgnoreQueryFilters()
        .Select(x => new { x.IsOnline })
        .ToListAsync();

      var totalUsers = users.Count;
      var onlineUsers = users.Count(x => x.IsOnline);

      var dto = new ServerStatsDto(
        totalTenants,
        onlineAgents,
        totalAgents,
        onlineUsers,
        totalUsers);

      return Result.Ok(dto);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<ServerStatsDto>(ex, "Error while getting server stats.")
        .Log(logger);
    }
  }
}