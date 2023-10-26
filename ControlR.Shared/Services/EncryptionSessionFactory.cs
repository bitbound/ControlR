using Microsoft.Extensions.Logging;

namespace ControlR.Shared.Services;

public interface IEncryptionSessionFactory
{
    IEncryptionSession CreateSession();
}

internal class EncryptionSessionFactory(ISystemTime systemTime, ILoggerFactory loggerFactory) : IEncryptionSessionFactory
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly ISystemTime _systemTime = systemTime;

    public IEncryptionSession CreateSession()
    {
        return new EncryptionSession(_systemTime, _loggerFactory.CreateLogger<EncryptionSession>());
    }
}