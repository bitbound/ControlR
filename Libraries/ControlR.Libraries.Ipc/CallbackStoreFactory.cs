using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Ipc;

public interface ICallbackStoreFactory
{
  ICallbackStore Create();
}

public class CallbackStoreFactory(IContentTypeResolver contentTypeResolver, ILoggerFactory loggerFactory) : ICallbackStoreFactory
{
  private readonly IContentTypeResolver _contentTypeResolver = contentTypeResolver;
  private readonly ILoggerFactory _loggerFactory = loggerFactory;

  public ICallbackStore Create()
  {
    var logger = _loggerFactory.CreateLogger<CallbackStore>();
    return new CallbackStore(_contentTypeResolver, logger);
  }
}