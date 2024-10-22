using System.Text.Json;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Converters;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ControlR.Web.Server.Data;

public class AppDb(DbContextOptions<AppDb> options)
  : IdentityDbContext<AppUser, IdentityRole<int>, int>(options)
{
  private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Default;

  private static readonly ValueComparer<List<Drive>> _driveListComparer = new(
    (a, b) => (a ?? new List<Drive>()).SequenceEqual(b ?? new List<Drive>()),
    c => c.Aggregate(0, (a, b) => HashCode.Combine(a, b.GetHashCode())),
    c => c.ToList());

  public DbSet<Device> Devices { get; init; }

  public DbSet<DeviceGroup> DeviceGroups { get; init; }

  public DbSet<Tenant> Tenants { get; init; }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    builder
      .Entity<IdentityRole<int>>()
      .HasData(
        new IdentityRole<int>()
        {
          Id = 1,
          Name = RoleNames.ServerAdministrator,
          NormalizedName = RoleNames.ServerAdministrator.ToUpper()
        });

    builder
      .Entity<IdentityRole<int>>()
      .HasData(
        new IdentityRole<int>()
        {
          Id = 2,
          Name = RoleNames.TenantAdministrator,
          NormalizedName = RoleNames.TenantAdministrator.ToUpper()
        });

    builder
    .Entity<IdentityRole<int>>()
    .HasData(
      new IdentityRole<int>()
      {
        Id = 3,
        Name = RoleNames.DeviceSuperUser,
        NormalizedName = RoleNames.DeviceSuperUser.ToUpper()
      });

    builder
      .Entity<Device>()
      .Property(x => x.Drives)
      .HasConversion(
        x => JsonSerializer.Serialize(x, _jsonOptions),
        x => JsonSerializer.Deserialize<List<Drive>>(x, _jsonOptions) ?? new List<Drive>(),
        _driveListComparer);

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
          .Property(nameof(EntityBase.Id))
          .HasDefaultValueSql("gen_random_uuid()");
      }

      var properties = entityType.ClrType
        .GetProperties()
        .Where(p =>
            p.PropertyType == typeof(DateTimeOffset) ||
            p.PropertyType == typeof(DateTimeOffset?));

      foreach (var property in properties)
      {
        builder
          .Entity(entityType.Name)
          .Property(property.Name)
          .HasConversion(new PostgresDateTimeOffsetConverter());
      }
    }
  }
}