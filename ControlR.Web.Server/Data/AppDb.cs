using ControlR.Web.Server.Data.Configuration;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ControlR.Web.Server.Data;

public class AppDb : IdentityUserContext<AppUser, Guid>, IDataProtectionKeyContext
{
  // EF Core's fluent API cannot express conditional check constraints or partial unique
  // indexes. These SQL strings assume default column mapping (property name == column name).
  private static readonly string _serverKindFilter =
    $"\"{nameof(ServiceAccount.Kind)}\" = '{nameof(ServiceAccountKind.Server)}' " +
    $"AND \"{nameof(ServiceAccount.TenantId)}\" IS NULL";
  private static readonly string _tenantKindFilter =
    $"\"{nameof(ServiceAccount.Kind)}\" = '{nameof(ServiceAccountKind.Tenant)}' " +
    $"AND \"{nameof(ServiceAccount.TenantId)}\" IS NOT NULL";

  private readonly Guid? _tenantId;
  private readonly Guid? _userId;

  public AppDb(DbContextOptions<AppDb> options) : base(options)
  {
    var extension = options.FindExtension<ClaimsDbContextOptionsExtension>();
    _tenantId = extension?.Options.TenantId;
    _userId = extension?.Options.UserId;
  }

  public DbSet<AgentInstallerKey> AgentInstallerKeys { get; init; }
  public DbSet<AgentInstallerKeyUsage> AgentInstallerKeyUsages { get; init; }
  public DbSet<AuthorizationChangeLog> AuthorizationChangeLogs { get; init; }
  public DbSet<Customer> Customers { get; init; }
  public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
  public DbSet<DeviceGroupMember> DeviceGroupMembers { get; init; }
  public DbSet<DeviceGroup> DeviceGroups { get; init; }
  public DbSet<Device> Devices { get; init; }
  public DbSet<LogonToken> LogonTokens { get; init; }
  public DbSet<PermissionAssignment> PermissionAssignments { get; init; }
  public DbSet<PersonalAccessToken> PersonalAccessTokens { get; init; }
  public DbSet<ServerAlert> ServerAlerts { get; init; }
  public DbSet<ServiceAccountCredential> ServiceAccountCredentials { get; init; }
  public DbSet<ServiceAccount> ServiceAccounts { get; init; }
  public DbSet<Tag> Tags { get; init; }
  public DbSet<TenantInvite> TenantInvites { get; init; }
  public DbSet<Tenant> Tenants { get; init; }
  public DbSet<TenantSetting> TenantSettings { get; init; }
  public DbSet<UserGroupMember> UserGroupMembers { get; init; }
  public DbSet<UserGroup> UserGroups { get; init; }
  public DbSet<UserPreference> UserPreferences { get; init; }
  public DbSet<UserStorageItem> UserStorageItems { get; init; }

  internal Guid? TenantId => _tenantId;
  internal Guid? UserId => _userId;

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
    ConfigureTags(builder);
    ConfigureTenantSettings(builder);
    ConfigureUsers(builder);
    ConfigureUserPreferences(builder);
    ConfigureUserStorage(builder);
    ConfigureTenantInvites(builder);
    ConfigureAgentInstallerKeys(builder);
    ConfigureAgentInstallerKeyUsages(builder);
    ConfigureServiceAccounts(builder);
    ConfigureDeviceGroups(builder);
    ConfigureCustomers(builder);
    ConfigureUserGroups(builder);
    ConfigurePermissionAssignments(builder);
    ConfigureAuthorizationChangeLogs(builder);
    ConfigureLogonTokens(builder);
  }

  private static void ConfigureAuthorizationChangeLogs(ModelBuilder builder)
  {
    builder
      .Entity<AuthorizationChangeLog>()
      .HasIndex(x => x.OwningTenantId);

    builder
      .Entity<AuthorizationChangeLog>()
      .HasIndex(x => x.CreatedAt);

    builder
      .Entity<AuthorizationChangeLog>()
      .HasIndex(x => x.ActorPrincipalId);

    builder
      .Entity<AuthorizationChangeLog>()
      .HasIndex(x => x.TargetId);
  }

  private static void ConfigurePermissionAssignments(ModelBuilder builder)
  {
    // PermissionAssignments intentionally have NO tenant query filter. Server-scoped rows
    // carry a null OwningTenantId, visibility is actor-capability-dependent (server.admin
    // holders also see null-owned rows), and the rule resolver must load rows without a
    // tenant context. Tenant isolation for this table is enforced in
    // PermissionAssignmentManager via IsVisibleToTenant. Note: the manager's
    // IgnoreQueryFilters() calls on this DbSet are currently no-ops and would silently
    // bypass a filter if one were ever added here.

    builder
      .Entity<PermissionAssignment>()
      .Property(x => x.PrincipalKind)
      .HasConversion<string>()
      .HasMaxLength(50);

    builder
      .Entity<PermissionAssignment>()
      .Property(x => x.Effect)
      .HasConversion<string>()
      .HasMaxLength(20);

    builder
      .Entity<PermissionAssignment>()
      .Property(x => x.ScopeKind)
      .HasConversion<string>()
      .HasMaxLength(50);

    builder
      .Entity<PermissionAssignment>()
      .HasIndex(x => new { x.ScopeKind, x.ScopeId });

    builder
      .Entity<PermissionAssignment>()
      .HasIndex(x => new { x.PrincipalKind, x.PrincipalId });

    builder
      .Entity<PermissionAssignment>()
      .HasIndex(x => new
      {
        x.PrincipalKind,
        x.PrincipalId,
        x.PermissionName,
        x.ScopeKind,
        x.ScopeId,
        x.Effect
      })
      .IsUnique()
      .AreNullsDistinct(false);

    // Persistence invariants (P2-3). ScopeKind is stored as its string name (HasConversion<string>()).
    // These constraints reject invalid scope/ownership combinations at the DB boundary, so a row
    // that bypasses the manager cannot be persisted in a state the evaluator would misauthorize.
    // ScopeKind: 'Server' = server scope (null ScopeId/OwningTenantId); 'Tenant' = tenant; the
    // remainder are resource scopes (Device/DeviceGroup/CustomerTenant/UserGroup) which require
    // a concrete ScopeId. 'Unknown' (sentinel, never persisted) is excluded from these checks.
    builder
      .Entity<PermissionAssignment>()
      .HasCheckConstraint(
        "CA_PermissionAssignments_Server_NullScope",
        "\"ScopeKind\" <> 'Server' OR (\"ScopeId\" IS NULL AND \"OwningTenantId\" IS NULL)");

    builder
      .Entity<PermissionAssignment>()
      .HasCheckConstraint(
        "CA_PermissionAssignments_NonServer_OwningTenant",
        "\"ScopeKind\" IN ('Unknown', 'Server') OR \"OwningTenantId\" IS NOT NULL");

    builder
      .Entity<PermissionAssignment>()
      .HasCheckConstraint(
        "CA_PermissionAssignments_ScopeKind_ScopeId",
        "\"ScopeKind\" NOT IN ('Tenant', 'Device', 'DeviceGroup', 'CustomerTenant', 'UserGroup') OR \"ScopeId\" IS NOT NULL");
  }

  private static void ConfigureServerAlert(ModelBuilder builder)
  {
    builder
      .Entity<ServerAlert>()
      .HasKey(x => x.Id);
  }

  private static List<int>? DeserializeDesktopSessionIds(string? value) =>
    string.IsNullOrWhiteSpace(value)
      ? null
      : JsonSerializer.Deserialize<List<int>>(value) ?? [];

  private static void SeedDatabase(ModelBuilder builder)
  {
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

  private static string? SerializeDesktopSessionIds(IReadOnlyList<int>? values) =>
    values is null ? null : JsonSerializer.Serialize(values);

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

  private void ConfigureCustomers(ModelBuilder builder)
  {
    builder
      .Entity<Customer>()
      .HasIndex(x => new { x.TenantId, x.Name })
      .IsUnique();

    builder
      .Entity<Device>()
      .HasOne(x => x.Customer)
      .WithMany()
      .HasForeignKey(x => x.CustomerId)
      .OnDelete(DeleteBehavior.SetNull);

    if (_tenantId is not null)
    {
      builder
        .Entity<Customer>()
        .HasQueryFilter(x => x.TenantId == _tenantId);
    }
  }

  private void ConfigureDeviceGroups(ModelBuilder builder)
  {
    builder
      .Entity<DeviceGroup>()
      .HasIndex(x => new { x.TenantId, x.Name })
      .IsUnique();

    builder
      .Entity<DeviceGroupMember>()
      .HasIndex(x => new { x.DeviceGroupId, x.DeviceId })
      .IsUnique();

    builder
      .Entity<DeviceGroupMember>()
      .HasOne(x => x.DeviceGroup)
      .WithMany(x => x.Members)
      .HasForeignKey(x => x.DeviceGroupId)
      .OnDelete(DeleteBehavior.Cascade);

    builder
      .Entity<DeviceGroupMember>()
      .HasOne(x => x.Device)
      .WithMany(x => x.DeviceGroupMembers)
      .HasForeignKey(x => x.DeviceId)
      .OnDelete(DeleteBehavior.Cascade);

    if (_tenantId is not null)
    {
      builder
        .Entity<DeviceGroup>()
        .HasQueryFilter(x => x.TenantId == _tenantId);

      builder
        .Entity<DeviceGroupMember>()
        .HasQueryFilter(x => x.Device != null && x.Device.TenantId == _tenantId);
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

  private void ConfigureLogonTokens(ModelBuilder builder)
  {
    builder
      .Entity<LogonToken>()
      .Property(x => x.AllowedDesktopSessionIds)
      .HasColumnType("jsonb")
      .HasConversion(new ValueConverter<IReadOnlyList<int>?, string?>(
        values => SerializeDesktopSessionIds(values),
        value => DeserializeDesktopSessionIds(value)))
      .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<int>?>(
        (left, right) => left == null
          ? right == null
          : right != null && left.SequenceEqual(right),
        values => values == null ? 0 : values.Aggregate(0, HashCode.Combine),
        values => values == null ? null : values.ToArray()));

    builder
      .Entity<LogonToken>()
      .HasIndex(x => x.Token)
      .IsUnique();

    builder
      .Entity<LogonToken>()
      .HasOne(x => x.Device)
      .WithMany(x => x.LogonTokens)
      .HasForeignKey(x => x.DeviceId)
      .OnDelete(DeleteBehavior.Cascade);

    builder
      .Entity<LogonToken>()
      .HasOne(x => x.User)
      .WithMany(x => x.LogonTokens)
      .HasForeignKey(x => x.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    if (_tenantId is not null)
    {
      builder
        .Entity<LogonToken>()
        .HasQueryFilter(x => x.TenantId == _tenantId);
    }
  }

  private void ConfigurePersonalAccessTokens(ModelBuilder builder)
  {
    builder
      .Entity<PersonalAccessToken>()
      .Property(x => x.PermissionMode)
      .HasConversion<string>()
      .HasMaxLength(50)
      .HasDefaultValue(PersonalAccessTokenPermissionMode.Restricted)
      .HasSentinel(PersonalAccessTokenPermissionMode.Restricted);

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

    if (_userId is not null)
    {
      builder
        .Entity<PersonalAccessToken>()
        .HasQueryFilter(x => x.UserId == _userId);
    }
    else if (_tenantId is not null)
    {
      builder
        .Entity<PersonalAccessToken>()
        .HasQueryFilter(x => x.User != null && x.User.TenantId == _tenantId);
    }
  }

  private void ConfigureServiceAccounts(ModelBuilder builder)
  {
    // Service accounts intentionally have NO tenant or user query filter. Authorization
    // evaluation and admin services must never accidentally hide relevant rows, and
    // server-scoped accounts do not map to the tenant filter model. Tenant-facing CRUD
    // applies tenant predicates explicitly in service code.

    builder
      .Entity<ServiceAccount>()
      .Property(x => x.Kind)
      .HasConversion<string>()
      .HasMaxLength(50);

    builder
      .Entity<ServiceAccount>()
      .Property(x => x.AccessMode)
      .HasConversion<string>()
      .HasMaxLength(50)
      .HasDefaultValue(ServiceAccountAccessMode.Restricted)
      .HasSentinel(ServiceAccountAccessMode.Restricted);

    builder
      .Entity<ServiceAccount>()
      .ToTable(t =>
      {
        t.HasCheckConstraint(
          "CK_ServiceAccounts_Kind_TenantId",
          $"(\"{nameof(ServiceAccount.Kind)}\" = '{nameof(ServiceAccountKind.Server)}' AND \"{nameof(ServiceAccount.TenantId)}\" IS NULL) OR " +
          $"(\"{nameof(ServiceAccount.Kind)}\" = '{nameof(ServiceAccountKind.Tenant)}' AND \"{nameof(ServiceAccount.TenantId)}\" IS NOT NULL)");
      });

    builder
      .Entity<ServiceAccount>()
      .HasIndex(x => x.Name)
      .HasDatabaseName("IX_ServiceAccounts_Server_Name")
      .IsUnique()
      .HasFilter(_serverKindFilter);

    builder
      .Entity<ServiceAccount>()
      .HasIndex(x => new { x.TenantId, x.Name })
      .HasDatabaseName("IX_ServiceAccounts_TenantId_Name")
      .IsUnique()
      .HasFilter(_tenantKindFilter);

    builder
      .Entity<ServiceAccount>()
      .HasOne(x => x.Tenant)
      .WithMany()
      .HasForeignKey(x => x.TenantId)
      .OnDelete(DeleteBehavior.Cascade);

    builder
      .Entity<ServiceAccount>()
      .HasMany(x => x.Credentials)
      .WithOne(x => x.ServiceAccount)
      .HasForeignKey(x => x.ServiceAccountId)
      .OnDelete(DeleteBehavior.Cascade);
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

    builder.Entity<Tenant>()
      .HasMany(t => t.Customers)
      .WithOne(c => c.Tenant)
      .HasForeignKey(c => c.TenantId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<Tenant>()
      .HasMany(t => t.DeviceGroups)
      .WithOne(dg => dg.Tenant)
      .HasForeignKey(dg => dg.TenantId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<Tenant>()
      .HasMany(t => t.UserGroups)
      .WithOne(ug => ug.Tenant)
      .HasForeignKey(ug => ug.TenantId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<Tenant>()
      .HasMany(t => t.LogonTokens)
      .WithOne(lt => lt.Tenant)
      .HasForeignKey(lt => lt.TenantId)
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

  private void ConfigureUserGroups(ModelBuilder builder)
  {
    builder
      .Entity<UserGroup>()
      .HasIndex(x => new { x.TenantId, x.Name })
      .IsUnique();

    builder
      .Entity<UserGroupMember>()
      .HasIndex(x => new { x.UserGroupId, x.UserId })
      .IsUnique();

    builder
      .Entity<UserGroupMember>()
      .HasOne(x => x.UserGroup)
      .WithMany(x => x.Members)
      .HasForeignKey(x => x.UserGroupId)
      .OnDelete(DeleteBehavior.Cascade);

    builder
      .Entity<UserGroupMember>()
      .HasOne(x => x.User)
      .WithMany(x => x.UserGroupMembers)
      .HasForeignKey(x => x.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    if (_tenantId is not null)
    {
      builder
        .Entity<UserGroup>()
        .HasQueryFilter(x => x.TenantId == _tenantId);

      builder
        .Entity<UserGroupMember>()
        .HasQueryFilter(x => x.User != null && x.User.TenantId == _tenantId);
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
    else if (_tenantId is not null)
    {
      builder
        .Entity<UserPreference>()
        .HasQueryFilter(x => x.User != null && x.User.TenantId == _tenantId);
    }
  }

  private void ConfigureUsers(ModelBuilder builder)
  {
    builder
      .Entity<AppUser>()
      .Property(x => x.AccountType)
      .HasConversion<string>()
      .HasMaxLength(50);

    builder
      .Entity<AppUser>()
      .Property(x => x.CreatedAt)
      .HasDefaultValueSql("CURRENT_TIMESTAMP");

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

  private void ConfigureUserStorage(ModelBuilder builder)
  {
    builder
      .Entity<UserStorageItem>()
      .HasIndex(x => new { x.Key, x.UserId })
      .IsUnique();

    builder
      .Entity<UserStorageItem>()
      .Property(x => x.Value)
      .HasMaxLength(UserStorageItem.MaxValueLength);

    builder
      .Entity<UserStorageItem>()
      .HasOne(x => x.User)
      .WithMany(u => u.UserStorageItems)
      .HasForeignKey(x => x.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    if (_userId is not null)
    {
      builder
        .Entity<UserStorageItem>()
        .HasQueryFilter(x => x.UserId == _userId);
    }
    else if (_tenantId is not null)
    {
      builder
        .Entity<UserStorageItem>()
        .HasQueryFilter(x => x.User != null && x.User.TenantId == _tenantId);
    }
  }
}