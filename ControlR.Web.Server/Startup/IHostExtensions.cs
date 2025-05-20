using ControlR.Web.Server.Authz.Roles;

namespace ControlR.Web.Server.Startup;

public static class HostExtensions
{
  public static async Task ApplyMigrations(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    await using var context = scope.ServiceProvider.GetRequiredService<AppDb>();
    if (context.Database.IsRelational())
    {
      await context.Database.MigrateAsync();
    }
  }

  public static async Task SetAllDevicesOffline(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    await using var context = scope.ServiceProvider.GetRequiredService<AppDb>();
    await context.Devices.ExecuteUpdateAsync(calls => calls.SetProperty(d => d.IsOnline, false));
  }

  public static async Task SetAllUsersOffline(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    await using var context = scope.ServiceProvider.GetRequiredService<AppDb>();
    await context.Users.ExecuteUpdateAsync(calls => calls.SetProperty(d => d.IsOnline, false));
  }

  public static async Task AddBuiltInRoles(this IHost host)
  {
    await using var scope = host.Services.CreateAsyncScope();
    await using var context = scope.ServiceProvider.GetRequiredService<AppDb>();
    var builtInRoles = RoleFactory.GetBuiltInRoles();
    await context.Roles.AddRangeAsync(builtInRoles);
    await context.SaveChangesAsync();
  }
}