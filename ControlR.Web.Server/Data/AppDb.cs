using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Server.Converters;
using ControlR.Web.Server.Data.Entities.Bases;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ControlR.Web.Server.Data;

public class AppDb(DbContextOptions<AppDb> options, IHttpContextAccessor httpContextAccessor)
  : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
  private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

  public DbSet<Device> Devices { get; init; }
  public DbSet<Tenant> Tenants { get; init; }
  public DbSet<Tag> Tags { get; init; }
  public DbSet<UserPreference> UserPreferences { get; init; }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    var tenantId = Guid.Empty;
    var userId = Guid.Empty;
    _ = _httpContextAccessor.HttpContext?.User.TryGetTenantId(out tenantId);
    _ = _httpContextAccessor.HttpContext?.User.TryGetUserId(out userId);
    
    SeedDatabase(builder);

    AddDeviceConfig(builder, tenantId);

    AddTagsConfig(builder, tenantId);
    
    AddUsersConfig(builder, tenantId);

    AddUserPreferenceConfig(builder, userId);

    ApplyReflectionBasedConfiguration(builder);
  }

  private static void AddUsersConfig(ModelBuilder builder, Guid tenantId)
  {
    if (tenantId != Guid.Empty)
    {
      builder
        .Entity<AppUser>()
        .HasQueryFilter(x => x.TenantId == tenantId);
    }
  }
  
  private static void AddDeviceConfig(ModelBuilder builder, Guid tenantId)
  {
    builder
      .Entity<Device>()
      .OwnsMany(x => x.Drives)
      .ToJson();

    if (tenantId != Guid.Empty)
    {
      builder
        .Entity<Device>()
        .HasQueryFilter(x => x.TenantId == tenantId);
    }
  }

  private static void AddTagsConfig(ModelBuilder builder, Guid tenantId)
  {
    builder
      .Entity<Tag>()
      .HasIndex(x => new { x.Name, x.TenantId })
      .IsUnique();
    
    if (tenantId != Guid.Empty)
    {
      builder
        .Entity<Tag>()
        .HasQueryFilter(x => x.TenantId == tenantId);
    }
  }

  private static void AddUserPreferenceConfig(ModelBuilder builder, Guid userId)
  {
    builder
      .Entity<UserPreference>()
      .HasIndex(x => new { x.Name, x.UserId })
      .IsUnique();

    if (userId != Guid.Empty)
    {
      builder
        .Entity<UserPreference>()
        .HasQueryFilter(x => x.UserId == userId);
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