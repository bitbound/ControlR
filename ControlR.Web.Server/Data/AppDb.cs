using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Server.Converters;
using ControlR.Web.Server.Data.Configuration;
using ControlR.Web.Server.Data.Entities.Bases;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ControlR.Web.Server.Data;

public class AppDb : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
  private readonly Guid? _tenantId;
  private readonly Guid? _userId;

  public AppDb(DbContextOptions<AppDb> options) : base(options)
  {
    var extension = options.FindExtension<ClaimsDbContextOptionsExtension>();
    _tenantId = extension?.Options.TenantId;
    _userId = extension?.Options.UserId;
  }

  public DbSet<Device> Devices { get; init; }
  public DbSet<Tenant> Tenants { get; init; }
  public DbSet<Tag> Tags { get; init; }
  public DbSet<UserPreference> UserPreferences { get; init; }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    SeedDatabase(builder);

    AddDeviceConfig(builder);

    AddTagsConfig(builder);
    
    AddUsersConfig(builder);

    AddUserPreferenceConfig(builder);

    ApplyReflectionBasedConfiguration(builder);
  }

  private void AddUsersConfig(ModelBuilder builder)
  {
    if (_tenantId is not null)
    {
      builder
        .Entity<AppUser>()
        .HasQueryFilter(x => x.TenantId == _tenantId);
    }
  }
  
  private void AddDeviceConfig(ModelBuilder builder)
  {
    builder
      .Entity<Device>()
      .OwnsMany(x => x.Drives)
      .ToJson();

    if (_tenantId is not null)
    {
      builder
        .Entity<Device>()
        .HasQueryFilter(x => x.TenantId == _tenantId);
    }
  }

  private void AddTagsConfig(ModelBuilder builder)
  {
    builder
      .Entity<Tag>()
      .HasIndex(x => new { x.Name, x.TenantId })
      .IsUnique();

    if (_tenantId is not null)
    {
      builder
        .Entity<Tag>()
        .HasQueryFilter(x => x.TenantId == _tenantId);
    }
  }

  private void AddUserPreferenceConfig(ModelBuilder builder)
  {
    builder
      .Entity<UserPreference>()
      .HasIndex(x => new { x.Name, x.UserId })
      .IsUnique();

    if (_userId is not null)
    {
      builder
        .Entity<UserPreference>()
        .HasQueryFilter(x => x.UserId == _userId);
    }
  }
  
  private static void ApplyReflectionBasedConfiguration(ModelBuilder builder)
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
          .HasConversion(new PostgresDateTimeOffsetConverter())
          .HasDefaultValueSql("CURRENT_TIMESTAMP");
      }
    }
  }
  
  private static void SeedDatabase(ModelBuilder builder)
  {
    builder
      .Entity<IdentityRole<Guid>>()
      .HasData(
        new IdentityRole<Guid>
        {
          Id = DeterministicGuid.Create(1),
          Name = RoleNames.ServerAdministrator,
          NormalizedName = RoleNames.ServerAdministrator.ToUpper()
        });

    builder
      .Entity<IdentityRole<Guid>>()
      .HasData(
        new IdentityRole<Guid>
        {
          Id = DeterministicGuid.Create(2),
          Name = RoleNames.TenantAdministrator,
          NormalizedName = RoleNames.TenantAdministrator.ToUpper()
        });

    builder
      .Entity<IdentityRole<Guid>>()
      .HasData(
        new IdentityRole<Guid>
        {
          Id = DeterministicGuid.Create(3),
          Name = RoleNames.DeviceSuperUser,
          NormalizedName = RoleNames.DeviceSuperUser.ToUpper()
        });
  }
}