using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ControlR.Web.Server.Data.Configuration;

public class ClaimsDbContextOptionsExtension(ClaimsDbContextOptions options) : IDbContextOptionsExtension
{
  private readonly ClaimsDbContextOptions _options = options;

  public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

  public void ApplyServices(IServiceCollection services) { }

  public void Validate(IDbContextOptions options) { }

  public ClaimsDbContextOptions Options => _options;

  private sealed class ExtensionInfo(ClaimsDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
  {
    private readonly ClaimsDbContextOptionsExtension _extension = extension;
    private readonly long _serviceProviderHash = HashCode.Combine(
          extension.Options.TenantId,
          extension.Options.UserId
      );
    private string? _logFragment;

    public override bool IsDatabaseProvider => false;

    public override string LogFragment
    {
      get
      {
        _logFragment ??= $"TenantId={_extension.Options.TenantId}";
        return _logFragment;
      }
    }

    public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
    {
      if (Extension is not ClaimsDbContextOptionsExtension extension) return;
      debugInfo["Tenant:Id"] = $"{extension.Options.TenantId}";
      debugInfo["User:Id"] = $"{extension.Options.UserId}";
    }

    public override int GetServiceProviderHashCode() => _serviceProviderHash.GetHashCode();

    public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
    {
      return other is ExtensionInfo otherInfo
          && _serviceProviderHash == otherInfo._serviceProviderHash;
    }
  }
}