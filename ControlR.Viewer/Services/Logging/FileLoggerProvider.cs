using Microsoft.Extensions.Logging;

namespace ControlR.Viewer.Services.Logging;
internal class FileLoggerProvider : ILoggerProvider
{

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}