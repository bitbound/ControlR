using System.Text.Json;
using ControlR.Web.Server.Converters;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ControlR.Web.Server.Data;

public class AppDb(DbContextOptions<AppDb> options)
  : IdentityDbContext<AppUser>(options)
{
  private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Default;

  private static readonly ValueComparer<string[]> _stringArrayComparer = new(
    (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
    c => c.Aggregate(0, (a, b) => HashCode.Combine(a, b.GetHashCode())),
    c => c.ToArray());

  private static readonly ValueComparer<List<Drive>> _driveListComparer = new(
    (a, b) => (a ?? new List<Drive>()).SequenceEqual(b ?? new List<Drive>()),
    c => c.Aggregate(0, (a, b) => HashCode.Combine(a, b.GetHashCode())),
    c => c.ToList());

  public DbSet<Device> Devices { get; init; }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    builder
      .Entity<Device>()
      .HasIndex(x => x.Uid)
      .IsUnique();

    builder
      .Entity<Device>()
      .Property(x => x.Uid)
      .HasDefaultValueSql("gen_random_uuid()")
      .HasSentinel(Guid.Empty);

    builder
      .Entity<Device>()
      .Property(x => x.CurrentUsers)
      .HasConversion(
        x => JsonSerializer.Serialize(x, _jsonOptions),
        x => JsonSerializer.Deserialize<string[]>(x, _jsonOptions) ?? Array.Empty<string>(),
        _stringArrayComparer);

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
          .Property(nameof(EntityBase.Uid))
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