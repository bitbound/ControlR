using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Authz.Roles;
using ControlR.Web.Server.Converters;
using ControlR.Web.Server.Data.Configuration;
using ControlR.Web.Server.Data.Entities.Bases;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Data;

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
  public required DbSet<ApiKey> ApiKeys { get; init; }
  public required DbSet<Tag> Tags { get; init; }
  public required DbSet<TenantInvite> TenantInvites { get; init; }
  public required DbSet<Tenant> Tenants { get; init; }
  public required DbSet<UserPreference> UserPreferences { get; init; }
  public required DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    SeedDatabase(builder);

    ConfigureApiKeys(builder);
    ConfigureTenant(builder);
    ConfigureDevices(builder);
    ConfigureRoles(builder);
    ConfigureTags(builder);
    ConfigureUsers(builder);
    ConfigureUserPreferences(builder);
    ConfigureTenantInvites(builder);
    ApplyReflectionBasedConfiguration(builder);
  }
  
  private void ConfigureTenant(ModelBuilder builder)
  {
    // Configure cascade delete for all related entities
    builder.Entity<Tenant>()
      .HasMany(t => t.Devices)
      .WithOne(d => d.Tenant)
      .HasForeignKey(d => d.TenantId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<Tenant>()
      .HasMany(t => t.Tags)
      .WithOne(tag => tag.Tenant)
      .HasForeignKey(tag => tag.TenantId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<Tenant>()
      .HasMany(t => t.Users)
      .WithOne(u => u.Tenant)
      .HasForeignKey(u => u.TenantId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<Tenant>()
      .HasMany(t => t.TenantInvites)
      .WithOne(invite => invite.Tenant)
      .HasForeignKey(invite => invite.TenantId)
      .OnDelete(DeleteBehavior.Cascade);
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

      var dateTimeOffsetProps = entityType.ClrType
        .GetProperties()
        .Where(p =>
          p.PropertyType == typeof(DateTimeOffset) ||
          p.PropertyType == typeof(DateTimeOffset?));

      foreach (var property in dateTimeOffsetProps)
      {
        var propertyBuilder = builder
          .Entity(entityType.Name)
          .Property(property.Name)
          .HasConversion(new PostgresDateTimeOffsetConverter());

        if (property.PropertyType == typeof(DateTimeOffset))
        {
          propertyBuilder.HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
      }
    }
  }

  private static void SeedDatabase(ModelBuilder builder)
  {
    var builtInRoles = RoleFactory.GetBuiltInRoles();

    builder
        .Entity<AppRole>()
        .HasData(builtInRoles);
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

  private void ConfigureApiKeys(ModelBuilder builder)
  {
    builder
      .Entity<ApiKey>()
      .HasIndex(x => x.HashedKey)
      .IsUnique();

    builder
      .Entity<ApiKey>()
      .Property(x => x.FriendlyName);

    builder
      .Entity<ApiKey>()
      .Property(x => x.HashedKey)
      .IsRequired();

    if (_tenantId is not null)
    {
      builder
        .Entity<ApiKey>()
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