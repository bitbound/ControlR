using System.Text.Json;
using ControlR.Web.Server.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Data;

public class AppDb(DbContextOptions<AppDb> options)
  : IdentityDbContext<AppUser>(options)
{
  private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Default;

  public DbSet<Device> Devices { get; set; }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    builder
      .Entity<Device>()
      .HasIndex(x => x.Uid);

    builder
      .Entity<Device>()
      .Property(x => x.CurrentUsers)
      .HasConversion(
        x => JsonSerializer.Serialize(x, _jsonOptions),
        x => JsonSerializer.Deserialize<string[]>(x, _jsonOptions) ?? Array.Empty<string>());
    
    builder
      .Entity<Device>()
      .Property(x => x.Drives)
      .HasConversion(
        x => JsonSerializer.Serialize(x, _jsonOptions),
        x => JsonSerializer.Deserialize<List<Drive>>(x, _jsonOptions) ?? new List<Drive>());

    foreach (var entityType in builder.Model.GetEntityTypes())
    {
      if (entityType.IsKeyless)
      {
        continue;
      }

      if (entityType.ClrType.BaseType == typeof(EntityBase))
      {
        builder
          .Entity(entityType.ClrType)
          .Property(nameof(EntityBase.Uid))
          .HasDefaultValueSql("gen_random_uuid()");
      }
    }
  }
}