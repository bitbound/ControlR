namespace ControlR.Web.Server.Services;

public interface IServerStatsProvider
{
  Task<Result<InternalDtos.ServerStatsDto>> GetServerStats();
}

public class ServerStatsProvider(
  IDbContextFactory<AppDb> dbContextFactory,
  ILogger<ServerStatsProvider> logger) : IServerStatsProvider
{
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;

  public async Task<Result<InternalDtos.ServerStatsDto>> GetServerStats()
  {
    try
    {
      await using var db = await _dbContextFactory.CreateDbContextAsync();

      var totalTenants = await db.Tenants.CountAsync();

      var agents = await db.Devices
        .AsNoTracking()
        .IgnoreQueryFilters()
        .Select(x => new { x.IsOnline })
        .ToListAsync();

      var totalAgents = agents.Count;
      var onlineAgents = agents.Count(x => x.IsOnline);

      var users = await db.Users
        .AsNoTracking()
        .IgnoreQueryFilters()
        .Select(x => new { x.IsOnline })
        .ToListAsync();

      var totalUsers = users.Count;
      var onlineUsers = users.Count(x => x.IsOnline);

      var dto = new InternalDtos.ServerStatsDto(
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
        .Fail<InternalDtos.ServerStatsDto>(ex, "Error while getting server stats.")
        .Log(logger);
    }
  }
}
