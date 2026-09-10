using Microsoft.Extensions.Options;

namespace ControlR.ApiClient.Internal;

/// <summary>
/// An <see cref="IOptionsMonitor{TOptions}"/> that hands out one immutable, pre-built value.
/// Per-target options are frozen at creation (first-config-wins), so change tracking is meaningless.
/// </summary>
internal sealed class FrozenOptionsMonitor<TOptions>(TOptions options) : IOptionsMonitor<TOptions>
{
  public TOptions CurrentValue => options;

  public TOptions Get(string? name) => options;

  public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
