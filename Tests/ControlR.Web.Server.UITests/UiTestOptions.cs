using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Web.Server.UITests;

/// <summary>
///   Configuration options for UI test sessions.
/// </summary>
internal sealed record UiTestOptions
{
  /// <summary>
  ///   The HTTPS base URL for the web server under test.
  /// </summary>
  public required Uri BaseUrl { get; init; }

  /// <summary>
  ///   The HTTP base URL used by the agent to connect to the server.
  /// </summary>
  public required Uri AgentServerUri { get; init; }

  /// <summary>
  ///   The semicolon-delimited list of server URLs passed to the web server process.
  /// </summary>
  public required string ServerUrls { get; init; }

  /// <summary>
  ///   The browser channel to use when launching Playwright (e.g. "chrome", "msedge").
  ///   When <c>null</c>, Playwright uses its bundled Chromium.
  /// </summary>
  public string? BrowserChannel { get; init; }

  /// <summary>
  ///   Whether to run the browser in headless mode.
  /// </summary>
  public required bool Headless { get; init; }

  /// <summary>
  ///   Directory where test screenshots and process logs are saved.
  /// </summary>
  public required DirectoryInfo ScreenshotDirectory { get; init; }

  /// <summary>
  ///   Default timeout for Playwright page operations.
  /// </summary>
  public required TimeSpan Timeout { get; init; }

  /// <summary>
  ///   The width of the browser viewport in pixels.
  /// </summary>
  public required int ViewportWidth { get; init; }

  /// <summary>
  ///   The height of the browser viewport in pixels.
  /// </summary>
  public required int ViewportHeight { get; init; }

  /// <summary>
  ///   Whether to start the Agent process as part of the test session.
  /// </summary>
  public required bool StartAgent { get; init; }

  /// <summary>
  ///   Whether to start the DesktopClient process as part of the test session.
  /// </summary>
  public required bool StartDesktopClient { get; init; }

  public static UiTestOptions Create(
    int viewportWidth = 1600,
    int viewportHeight = 1000,
    bool startAgent = false,
    bool startDesktopClient = false,
    bool headless = true,
    string? browserChannel = null)
  {
    var solutionDirResult = IoHelper.GetSolutionDir(AppContext.BaseDirectory);
    if (!solutionDirResult.IsSuccess || string.IsNullOrWhiteSpace(solutionDirResult.Value))
    {
      throw new InvalidOperationException("Unable to locate the solution directory for UI test screenshot output.");
    }

    var screenshotDirectoryPath = Path.Combine(solutionDirResult.Value, "TestResults", "ControlR.Web.Server.UITests");

    var httpsBaseUrl = new Uri($"https://localhost:{UiTestConstants.ServerHttpsPort}");
    var httpBaseUrl = new Uri($"http://localhost:{UiTestConstants.ServerHttpPort}");

    return new UiTestOptions
    {
      BaseUrl = httpsBaseUrl,
      AgentServerUri = httpBaseUrl,
      ServerUrls = $"{httpsBaseUrl};{httpBaseUrl}",
      BrowserChannel = browserChannel,
      Headless = headless,
      ScreenshotDirectory = new DirectoryInfo(Path.GetFullPath(screenshotDirectoryPath)),
      Timeout = TimeSpan.FromSeconds(30),
      ViewportWidth = viewportWidth,
      ViewportHeight = viewportHeight,
      StartAgent = startAgent,
      StartDesktopClient = startDesktopClient,
    };
  }

}
