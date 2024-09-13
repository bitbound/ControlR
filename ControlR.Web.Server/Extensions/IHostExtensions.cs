using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Extensions;

public static class IHostExtensions
{
    public static async Task ApplyMigrations<TDbContext>(this IHost host)
        where TDbContext : DbContext
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
    }
}
