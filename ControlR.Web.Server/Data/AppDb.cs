using ControlR.Web.Server.Authz.Roles;
using ControlR.Web.Server.Data.Configuration;
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

  public DbSet<AgentInstallerKeyUsage> AgentInstallerKeyUsages { get; init; }
  public DbSet<AgentInstallerKey> AgentInstallerKeys { get; init; }
  public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
  public DbSet<Device> Devices { get; init; }
  public DbSet<PersonalAccessToken> PersonalAccessTokens { get; init; }
  public DbSet<ServerAlert> ServerAlerts { get; init; }
  public DbSet<Tag> Tags { get; init; }
  public DbSet<TenantInvite> TenantInvites { get; init; }
  public DbSet<TenantSetting> TenantSettings { get; init; }
  public DbSet<Tenant> Tenants { get; init; }
  public DbSet<UserPreference> UserPreferences { get; init; }

  protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
  {
    base.ConfigureConventions(configurationBuilder);
    configurationBuilder.Conventions.Add(_ => new DateTimeOffsetConvention());
    configurationBuilder.Conventions.Add(_ => new EntityBaseConvention());
  }
  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    SeedDatabase(builder);

    ConfigurePersonalAccessTokens(builder);
    ConfigureServerAlert(builder);
    ConfigureTenant(builder);
    ConfigureDevices(builder);
    ConfigureRoles(builder);
    ConfigureTags(builder);
    ConfigureTenantSettings(builder);
    ConfigureUsers(builder);
    ConfigureUserPreferences(builder);
    ConfigureTenantInvites(builder);
    ConfigureAgentInstallerKeys(builder);
    ConfigureAgentInstallerKeyUsages(builder);
  }

  private static void ConfigureRoles(ModelBuilder builder)
  {
    builder
      .Entity<AppRole>()
      .HasMany(x => x.UserRoles)
      .WithOne()
      .HasForeignKey(x => x.RoleId);
  }
  private static void ConfigureServerAlert(ModelBuilder builder)
  {
    builder
      .Entity<ServerAlert>()
      .HasKey(x => x.Id);
  }
  private static void SeedDatabase(ModelBuilder builder)
  {
    var builtInRoles = RoleFactory.GetBuiltInRoles();

    builder
        .Entity<AppRole>()
        .HasData(builtInRoles);

    builder
        .Entity<ServerAlert>()
        .HasData(new ServerAlert
        {
          Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
          Message = string.Empty,
          Severity = MessageSeverity.Information,
          IsDismissable = true,
          IsSticky = false,
          IsEnabled = false
        });
  }

  private void ConfigureAgentInstallerKeyUsages(ModelBuilder builder)
  {
    builder
      .Entity<AgentInstallerKeyUsage>()
      .HasKey(x => x.Id);

    if (_tenantId is not null)
    {
      builder
        .Entity<AgentInstallerKeyUsage>()
        .HasQueryFilter(x => x.TenantId == _tenantId);
    }
  }
  private void ConfigureAgentInstallerKeys(ModelBuilder builder)
  {
    builder
      .Entity<AgentInstallerKey>()
      .HasMany(x => x.Usages)
      .WithOne(x => x.AgentInstallerKey)
      .HasForeignKey(x => x.AgentInstallerKeyId)
      .OnDelete(DeleteBehavior.Cascade);

    if (_tenantId is not null)
    {
      builder
        .Entity<AgentInstallerKey>()
        .HasQueryFilter(x => x.TenantId == _tenantId);
    }
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
  private void ConfigurePersonalAccessTokens(ModelBuilder builder)
  {
    builder
      .Entity<PersonalAccessToken>()
      .HasIndex(x => x.HashedKey)
      .IsUnique();

    builder
      .Entity<PersonalAccessToken>()
      .Property(x => x.HashedKey)
      .IsRequired();

    builder
      .Entity<PersonalAccessToken>()
      .HasOne(x => x.User)
      .WithMany(x => x.PersonalAccessTokens)
      .HasForeignKey(x => x.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    if (_tenantId is not null)
    {
      builder
        .Entity<PersonalAccessToken>()
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
      .HasMany(t => t.TenantSettings)
      .WithOne(setting => setting.Tenant)
      .HasForeignKey(setting => setting.TenantId)
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
  private void ConfigureTenantSettings(ModelBuilder builder)
  {
    builder
      .Entity<TenantSetting>()
      .HasIndex(x => new { x.Name, x.TenantId })
      .IsUnique();

    if (_tenantId is not null)
    {
      builder
        .Entity<TenantSetting>()
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
      .Property(x => x.CreatedAt)
      .HasDefaultValueSql("CURRENT_TIMESTAMP");

    builder
      .Entity<AppUser>()
      .HasMany(x => x.UserRoles)
      .WithOne()
      .HasForeignKey(x => x.UserId);

    builder
      .Entity<AppUser>()
      .HasMany(x => x.UserPreferences)
      .WithOne(x => x.User)
      .HasForeignKey(x => x.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    if (_tenantId is not null)
    {
      builder
        .Entity<AppUser>()
        .HasQueryFilter(x => x.TenantId == _tenantId);
    }
  }
}