using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.Ipc;

public static class IServiceCollectionExtensions
{
  public static IServiceCollection AddControlrIpc(this IServiceCollection services)
  {
    services.AddLogging();
    services.AddSingleton<IContentTypeResolver, ContentTypeResolver>();
    services.AddSingleton<IIpcConnectionFactory, IpcConnectionFactory>();
    services.AddSingleton<ICallbackStoreFactory, CallbackStoreFactory>();
    services.AddTransient<ICallbackStore, CallbackStore>();
    return services;
  }
}