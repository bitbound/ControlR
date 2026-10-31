using ControlR.Assist.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlR.Assist.Tests;

public class ServiceCollectionExtensionsTests
{
  // The log file is the artifact support asks for, so the registration has to produce a logger that
  // actually writes it. A build only proves the sink is referenced.
  [Fact]
  public void AddControlrAssist_LogsToTheAssistLogFile()
  {
    var services = new ServiceCollection();
    var directory = Path.GetDirectoryName(AssistSettingsService.LogFilePath) ?? throw new InvalidOperationException("The log path has no directory.");
    var fileName = Path.GetFileNameWithoutExtension(AssistSettingsService.LogFilePath);

    services.AddControlrAssist();

    using (var provider = services.BuildServiceProvider())
    {
      provider.GetRequiredService<ILogger<AssistSessionCoordinator>>()
        .LogInformation("Test marker {Marker}.", Guid.NewGuid());
    }

    // A daily roll inserts the date before the extension, so the written name is not LogFilePath.
    var written = Directory.Exists(directory) && Directory.EnumerateFiles(directory, fileName + "*.log").Any();
    Assert.True(written, $"No file named {fileName}*.log in {directory}.");
  }
}
