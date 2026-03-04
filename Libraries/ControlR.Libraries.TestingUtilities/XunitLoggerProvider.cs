using Microsoft.Extensions.Logging;
using Xunit;

namespace ControlR.Libraries.TestingUtilities;

public class XunitLoggerProvider(ITestOutputHelper testOutputHelper) : ILoggerProvider
{
  private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;
  public ILogger CreateLogger(string categoryName)
  {
    return new XunitLogger(_testOutputHelper, categoryName);
  }

  public void Dispose()
  {
    GC.SuppressFinalize(this);
  }
}