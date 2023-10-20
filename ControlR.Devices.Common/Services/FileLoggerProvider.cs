using Microsoft.Extensions.Logging;

namespace ControlR.Devices.Common.Services;

public class FileLoggerProvider(string componentName, string componentVersion) : ILoggerProvider
{
    private readonly string _componentName = componentName;
    private readonly string _componentVersion = componentVersion;

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(_componentName, _componentVersion, categoryName);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
