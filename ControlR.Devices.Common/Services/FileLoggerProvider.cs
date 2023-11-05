using Microsoft.Extensions.Logging;

namespace ControlR.Devices.Common.Services;

public class FileLoggerProvider(
    string _componentVersion,
    Func<string> _logPathFactory,
    TimeSpan _logRetention) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(_componentVersion, categoryName, _logPathFactory, _logRetention);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}