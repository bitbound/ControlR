using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.Web.Server.Services;

public class TemporaryUserCleanupService : BackgroundService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<TemporaryUserCleanupService> _logger;
  private readonly TimeProvider _timeProvider;

  public TemporaryUserCleanupService(
    IServiceProvider serviceProvider,
    ILogger<TemporaryUserCleanupService> logger,
    TimeProvider timeProvider)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
    _timeProvider = timeProvider;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await CleanupExpiredTemporaryUsers();
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Check every 5 minutes
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during temporary user cleanup");
      }
    }
  }

  private async Task CleanupExpiredTemporaryUsers()
  {
    using var scope = _serviceProvider.CreateScope();
    using var dbContext = scope.ServiceProvider.GetRequiredService<AppDb>();
    
    var now = _timeProvider.GetUtcNow();
    var expiredUsers = await dbContext.Users
      .Include(u => u.UserPreferences) // Ensure preferences are loaded for cascade delete
      .Where(u => u.IsTemporary && u.TemporaryUserExpiresAt <= now)
      .ToListAsync();

    if (expiredUsers.Count > 0)
    {
      dbContext.Users.RemoveRange(expiredUsers);
      await dbContext.SaveChangesAsync();
      
      _logger.LogInformation(
        "Cleaned up {Count} expired temporary users",
        expiredUsers.Count);
    }
  }
}
