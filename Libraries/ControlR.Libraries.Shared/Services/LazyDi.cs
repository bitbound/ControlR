using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.Shared.Services;

/// <summary>
/// <para>
///   When Blazor prerenders components, the DI context will be on the server,
///   so client-side services won't be registered.  This causes an exception
///   to be thrown when client-side services are directly injected into components.
/// </para>
/// <para>
///   Wrapping the service in this interface allows it to be lazy-loaded after
///   checking <see cref="OperatingSystem.IsBrowser"/>.
/// </para>
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ILazyDi<T>
{
    public bool Exists { get; }
    public T? Maybe { get; }
    public T Value { get; }
}

public class LazyDi<T>(IServiceProvider _serviceProvider) : Lazy<T>(
        valueFactory: _serviceProvider.GetRequiredService<T>,
        isThreadSafe: true), ILazyDi<T> where T : class
{
    public bool Exists
    {
        get
        {
            return _serviceProvider.GetService<T>() is not null;
        }
    }

    public T? Maybe => _serviceProvider.GetService<T>();
}
