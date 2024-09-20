using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.Shared.Services;

/// <summary>
///   <para>
///     When Blazor prerenders components, the DI context will be on the server,
///     so client-side services won't be registered.  This causes an exception
///     to be thrown when client-side services are directly injected into components.
///   </para>
///   <para>
///     Wrapping the service in this interface allows it to be lazy-loaded after
///     checking <see cref="OperatingSystem.IsBrowser" />.
///   </para>
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ILazyDi<T>
{
  [MemberNotNullWhen(true, nameof(Maybe))]
  bool Exists { get; }

  T? Maybe { get; }
  T Value { get; }
}

public class LazyDi<T>(IServiceProvider serviceProvider) : Lazy<T>(
  serviceProvider.GetRequiredService<T>,
  true), ILazyDi<T> where T : class
{
  [MemberNotNullWhen(true, nameof(Maybe))]
  public bool Exists => serviceProvider.GetService<T>() is not null;

  public T? Maybe => serviceProvider.GetService<T>();
}