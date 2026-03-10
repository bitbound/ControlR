namespace ControlR.Libraries.Shared.Services.Logging;

public class FileLoggerProvider(
  string componentVersion,
  Func<string> logPathFactory,
  TimeSpan logRetention) : ILoggerProvider
{
  public ILogger CreateLogger(string categoryName)
  {
    return new FileLogger(componentVersion, categoryName, logPathFactory, logRetention);
  }

  public void Dispose()
  {
    GC.SuppressFinalize(this);
  }
}