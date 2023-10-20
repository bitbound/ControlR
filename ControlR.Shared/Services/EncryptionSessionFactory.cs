using Microsoft.Extensions.Logging;

namespace ControlR.Shared.Services;

public interface IEncryptionSessionFactory
{
    IEncryptionSession CreateSession();
}

internal class EncryptionSessionFactory(ILoggerFactory loggerFactory) : IEncryptionSessionFactory
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public IEncryptionSession CreateSession()
    {
        return new EncryptionSession(_loggerFactory.CreateLogger<EncryptionSession>());
    }
}
