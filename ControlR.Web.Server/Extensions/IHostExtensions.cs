using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Extensions;

public static class HostExtensions
{
  public static async Task ApplyMigrations(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    using var context = scope.ServiceProvider.GetRequiredService<AppDb>();
    if (context.Database.IsRelational())
    {
      await context.Database.MigrateAsync();
    }
  }

  public static async Task SetAllDevicesOffline(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    await using var context = scope.ServiceProvider.GetRequiredService<AppDb>();
    context.Devices.ExecuteUpdate(calls => calls.SetProperty(d => d.IsOnline, false));
  }
}