using System.Text.Json;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Converters;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ControlR.Web.Server.Data;

public class AppDb(DbContextOptions<AppDb> options)
  : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
  private static readonly ValueComparer<List<Drive>> _driveListComparer = new(
    (a, b) => (a ?? new List<Drive>()).SequenceEqual(b ?? new List<Drive>()),
    c => c.Aggregate(0, (a, b) => HashCode.Combine(a, b.GetHashCode())),
    c => c.ToList());

  private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Default;
  public DbSet<DeviceGroup> DeviceGroups { get; init; }
  public DbSet<Device> Devices { get; init; }
  public DbSet<Tenant> Tenants { get; init; }

  public DbSet<UserPreference> UserPreferences { get; init; }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    AddSeedData(builder);

    AddDeviceConfig(builder);

    AddUserPreferenceConfig(builder);

    AddDateTimeConversions(builder);
  }

  private static void AddUserPreferenceConfig(ModelBuilder builder)
  {
    builder
      .Entity<UserPreference>()
      .HasIndex(x => x.Name)
      .IsUnique();
  }

  private static void AddDateTimeConversions(ModelBuilder builder)
  {
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

  private static void AddDeviceConfig(ModelBuilder builder)
  {
    builder
      .Entity<Device>()
      .Property(x => x.Drives)
      .HasConversion(
          x => JsonSerializer.Serialize(x, _jsonOptions),
          x => JsonSerializer.Deserialize<List<Drive>>(x, _jsonOptions) ?? new List<Drive>(),
          _driveListComparer);
  }

  private static void AddSeedData(ModelBuilder builder)
  {
    builder
        .Entity<IdentityRole<Guid>>()
        .HasData(
          new IdentityRole<Guid>()
          {
            Id = GuidHelper.CreateDeterministicGuid(1),
            Name = RoleNames.ServerAdministrator,
            NormalizedName = RoleNames.ServerAdministrator.ToUpper()
          });

    builder
      .Entity<IdentityRole<Guid>>()
      .HasData(
        new IdentityRole<Guid>()
        {
          Id = GuidHelper.CreateDeterministicGuid(2),
          Name = RoleNames.TenantAdministrator,
          NormalizedName = RoleNames.TenantAdministrator.ToUpper()
        });

    builder
    .Entity<IdentityRole<Guid>>()
    .HasData(
      new IdentityRole<Guid>()
      {
        Id = GuidHelper.CreateDeterministicGuid(3),
        Name = RoleNames.DeviceSuperUser,
        NormalizedName = RoleNames.DeviceSuperUser.ToUpper()
      });
  }
}