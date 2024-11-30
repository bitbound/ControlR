using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Converters;
using ControlR.Web.Server.Data.Configuration;
using ControlR.Web.Server.Data.Entities.Bases;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ControlR.Web.Server.Data;

public class AppDb : IdentityDbContext<AppUser, AppRole, Guid>, IDataProtectionKeyContext
{
  private readonly Guid? _tenantId;
  private readonly Guid? _userId;

  public AppDb(DbContextOptions<AppDb> options) : base(options)
  {
    var extension = options.FindExtension<ClaimsDbContextOptionsExtension>();
    _tenantId = extension?.Options.TenantId;
    _userId = extension?.Options.UserId;
  }

  public required DbSet<Device> Devices { get; init; }
  public required DbSet<Tag> Tags { get; init; }
  public required DbSet<TenantInvite> TenantInvites { get; init; }
  public required DbSet<Tenant> Tenants { get; init; }
  public required DbSet<UserPreference> UserPreferences { get; init; }
  public required DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    SeedDatabase(builder);

    ConfigureDevices(builder);

    ConfigureRoles(builder);

    ConfigureTags(builder);
    
    ConfigureUsers(builder);

    ConfigureUserPreferences(builder);

    ConfigureTenantInvites(builder);

    ApplyReflectionBasedConfiguration(builder);
  }

  private void ConfigureTenantInvites(ModelBuilder builder)
  {
    builder
      .Entity<TenantInvite>()
      .HasIndex(x => x.ActivationCode);

    if (_tenantId is not null)
    {
      builder
        .Entity<TenantInvite>()
        .HasQueryFilter(x => x.TenantId == _tenantId);
    }
  }

  private static void ConfigureRoles(ModelBuilder builder)
  {
    builder
      .Entity<AppRole>()
      .HasMany(x => x.UserRoles)
      .WithOne()
      .HasForeignKey(x => x.RoleId);
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
      .Entity<AppRole>()
      .HasData(
        new AppRole
        {
          Id = DeterministicGuid.Create(1),
          Name = RoleNames.ServerAdministrator,
          NormalizedName = RoleNames.ServerAdministrator.ToUpper()
        });

    builder
      .Entity<AppRole>()
      .HasData(
        new AppRole
        {
          Id = DeterministicGuid.Create(2),
          Name = RoleNames.TenantAdministrator,
          NormalizedName = RoleNames.TenantAdministrator.ToUpper()
        });

    builder
      .Entity<AppRole>()
      .HasData(
        new AppRole
        {
          Id = DeterministicGuid.Create(3),
          Name = RoleNames.DeviceSuperUser,
          NormalizedName = RoleNames.DeviceSuperUser.ToUpper()
        });
  }

  private void ConfigureDevices(ModelBuilder builder)
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

  private void ConfigureTags(ModelBuilder builder)
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

  private void ConfigureUserPreferences(ModelBuilder builder)
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

  private void ConfigureUsers(ModelBuilder builder)
  {
    builder
      .Entity<AppUser>()
      .HasMany(x => x.UserRoles)
      .WithOne()
      .HasForeignKey(x => x.UserId);

    if (_tenantId is not null)
    {
      builder
        .Entity<AppUser>()
        .HasQueryFilter(x => x.TenantId == _tenantId);
    }
  }
}