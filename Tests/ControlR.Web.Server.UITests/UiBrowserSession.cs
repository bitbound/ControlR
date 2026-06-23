using System.Collections.Concurrent;
using System.Diagnostics;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Playwright;
using Testcontainers.PostgreSql;

namespace ControlR.Web.Server.UITests;

internal sealed class UiBrowserSession : IAsyncDisposable
{
  private static readonly SemaphoreSlim _playwrightInstallLock = new(1, 1);

  private static bool _isPlaywrightInstalled;

  private readonly IBrowser _browser;
  private readonly UiTestDatabaseSession _databaseSession;
  private readonly IPlaywright _playwright;
  private readonly List<UiTestProcessSession> _startedProcesses;

  private UiBrowserSession(
    UiTestDatabaseSession databaseSession,
    List<UiTestProcessSession> startedProcesses,
    UiTestOptions options,
    IPlaywright playwright,
    IBrowser browser,
    IBrowserContext browserContext,
    IPage page)
  {
    Options = options;
    _databaseSession = databaseSession;
    _playwright = playwright;
    _browser = browser;
    BrowserContext = browserContext;
    Page = page;
    _startedProcesses = startedProcesses;
  }

  public IBrowserContext BrowserContext { get; }
  public UiTestOptions Options { get; }
  public IPage Page { get; }

  public static async Task<UiBrowserSession> Create(
    int viewportWidth = 1600,
    int viewportHeight = 1000,
    bool startAgent = false,
    bool startDesktopClient = false,
    bool headless = true,
    string? browserChannel = null)
  {
    var options = UiTestOptions.Create(viewportWidth, viewportHeight, startAgent, startDesktopClient, headless, browserChannel);
    var databaseSession = await UiTestDatabaseSession.Create();

    var startedProcesses = new List<UiTestProcessSession>();
    IPlaywright? playwright = null;
    IBrowser? browser = null;
    IBrowserContext? browserContext = null;

    try
    {
      await EnsurePlaywrightInstalled();

      var serverSession = await EnsureServerAvailabilityAsync(options, databaseSession);
      startedProcesses.Add(serverSession);

      if (options.StartAgent)
      {
        var agentSession = await EnsureAgentAvailabilityAsync(options);
        startedProcesses.Add(agentSession);
      }

      if (options.StartDesktopClient)
      {
        var desktopClientSession = await EnsureDesktopClientAvailabilityAsync(options);
        startedProcesses.Add(desktopClientSession);
      }

      playwright = await Playwright.CreateAsync();

      var launchOptions = new BrowserTypeLaunchOptions
      {
        Channel = options.BrowserChannel,
        Headless = options.Headless,
      };

      browser = await playwright.Chromium.LaunchAsync(launchOptions);
      browserContext = await browser.NewContextAsync(new BrowserNewContextOptions
      {
        BaseURL = options.BaseUrl.ToString(),
        IgnoreHTTPSErrors = true,
        ViewportSize = new ViewportSize
        {
          Width = options.ViewportWidth,
          Height = options.ViewportHeight,
        },
      });

      var page = await browserContext.NewPageAsync();
      page.SetDefaultTimeout((float)options.Timeout.TotalMilliseconds);

      return new UiBrowserSession(databaseSession, startedProcesses, options, playwright, browser, browserContext, page);
    }
    catch
    {
      if (browserContext is not null)
      {
        await browserContext.DisposeAsync();
      }

      if (browser is not null)
      {
        await browser.DisposeAsync();
      }

      playwright?.Dispose();

      await StopProcesses(startedProcesses, options, persistLogs: true);

      await databaseSession.DisposeAsync();

      throw;
    }
  }

  public async ValueTask DisposeAsync()
  {
    await BrowserContext.DisposeAsync();
    await _browser.DisposeAsync();
    _playwright.Dispose();

    await StopProcesses(_startedProcesses, Options, persistLogs: false);

    await _databaseSession.DisposeAsync();
  }

  public async Task SaveScreenshot(string fileName)
  {
    Directory.CreateDirectory(Options.ScreenshotDirectory.FullName);
    var filePath = Path.Combine(Options.ScreenshotDirectory.FullName, $"{fileName}.png");

    await Page.ScreenshotAsync(new PageScreenshotOptions
    {
      FullPage = true,
      Path = filePath,
    });
  }

  private static async Task<UiTestProcessSession> EnsureAgentAvailabilityAsync(UiTestOptions options)
  {
    var agentProjectPath = GetProjectPath("ControlR.Agent", "ControlR.Agent.csproj");
    var outputLog = new ConcurrentQueue<string>();

    var processSession = StartDotNetProcess(
      processName: "agent",
      projectPath: agentProjectPath,
      launchProfile: "Run",
      outputLog: outputLog,
      argumentsAfterDoubleDash: [],
      environmentVariables: new Dictionary<string, string>
      {
        ["AppOptions__ServerUri"] = options.AgentServerUri.ToString(),
      });

    await EnsureProcessRemainsRunning(options, processSession, TimeSpan.FromSeconds(5));
    return processSession;
  }

  private static async Task<UiTestProcessSession> EnsureDesktopClientAvailabilityAsync(UiTestOptions options)
  {
    var desktopClientProjectPath = GetProjectPath("ControlR.DesktopClient", "ControlR.DesktopClient.csproj");
    var outputLog = new ConcurrentQueue<string>();

    var processSession = StartDotNetProcess(
      processName: "desktopclient",
      projectPath: desktopClientProjectPath,
      launchProfile: "Run",
      outputLog: outputLog,
      argumentsAfterDoubleDash: [],
      environmentVariables: null);

    await EnsureProcessRemainsRunning(options, processSession, TimeSpan.FromSeconds(5));
    return processSession;
  }

  private static async Task EnsurePlaywrightInstalled()
  {
    if (_isPlaywrightInstalled)
    {
      return;
    }

    await _playwrightInstallLock.WaitAsync();

    try
    {
      if (_isPlaywrightInstalled)
      {
        return;
      }

      // First try installing with system dependencies (requires sudo on Linux).
      var retVal = await Task.Run(() => Microsoft.Playwright.Program.Main(["install", "--with-deps", "chromium"]));
      if (retVal != 0)
      {
        // --with-deps often fails on non-Debian distros (Fedora, Arch, etc.)
        // because it needs sudo and the package list is Debian-centric.
        // Fall back to installing just the browser binaries.
        retVal = await Task.Run(() => Microsoft.Playwright.Program.Main(["install", "chromium"]));
      }

      if (retVal != 0)
      {
        throw new InvalidOperationException($"Playwright installation failed with exit code {retVal}.");
      }

      _isPlaywrightInstalled = true;
    }
    finally
    {
      _playwrightInstallLock.Release();
    }
  }

  private static async Task EnsureProcessRemainsRunning(
    UiTestOptions options,
    UiTestProcessSession processSession,
    TimeSpan timeout)
  {
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
      if (!processSession.Process.HasExited)
      {
        await Task.Delay(250);
        continue;
      }

      await PersistProcessLog(options, processSession.Name, processSession.OutputLog);
      throw new InvalidOperationException(
        $"{processSession.Name} process exited during startup. ExitCode={processSession.Process.ExitCode}{Environment.NewLine}{FormatServerLog(processSession.OutputLog)}");
    }

    if (!processSession.Process.HasExited)
    {
      return;
    }

    await PersistProcessLog(options, processSession.Name, processSession.OutputLog);
    throw new InvalidOperationException(
      $"{processSession.Name} process exited during startup. ExitCode={processSession.Process.ExitCode}{Environment.NewLine}{FormatServerLog(processSession.OutputLog)}");
  }

  private static async Task<UiTestProcessSession> EnsureServerAvailabilityAsync(
    UiTestOptions options,
    UiTestDatabaseSession databaseSession)
  {
    var baseUrl = options.BaseUrl;
    var serverProjectPath = GetProjectPath("ControlR.Web.Server", "ControlR.Web.Server.csproj");
    var outputLog = new ConcurrentQueue<string>();

    var processSession = StartDotNetProcess(
      processName: "server",
      projectPath: serverProjectPath,
      launchProfile: "https",
      outputLog: outputLog,
      argumentsAfterDoubleDash: ["--urls", options.ServerUrls],
      environmentVariables: new Dictionary<string, string>
      {
        ["ASPNETCORE_ENVIRONMENT"] = "Testing",
        ["DOTNET_ENVIRONMENT"] = "Testing",
        ["ControlR_POSTGRES_HOST"] = databaseSession.Host,
        ["ControlR_POSTGRES_PORT"] = databaseSession.Port.ToString(),
        ["ControlR_POSTGRES_DB"] = databaseSession.DatabaseName,
        ["Bootstrap:AdminEmail"] = UiTestConstants.BootstrapAdminEmail,
        ["Bootstrap:AdminPassword"] = UiTestConstants.BootstrapAdminPassword,
        ["AppOptions:DisableEmailSending"] = "true",
      });

    await WaitForServerReadyAsync(options, TimeSpan.FromSeconds(120), processSession);
    return processSession;
  }

  private static string FormatServerLog(ConcurrentQueue<string> outputLog)
  {
    return string.Join(Environment.NewLine, outputLog);
  }

  private static string GetProjectPath(
    string projectDirectoryName,
    string projectFileName)
  {
    var solutionDirResult = IoHelper.GetSolutionDir(AppContext.BaseDirectory);
    if (!solutionDirResult.IsSuccess || string.IsNullOrWhiteSpace(solutionDirResult.Value))
    {
      throw new InvalidOperationException("Unable to locate the solution directory to start UI test processes.");
    }

    var projectDirectory = Path.Combine(solutionDirResult.Value, projectDirectoryName);
    if (!Directory.Exists(projectDirectory))
    {
      throw new InvalidOperationException($"Unable to determine the working directory for {projectDirectoryName}.");
    }

    var projectPath = Path.Combine(projectDirectory, projectFileName);
    if (!File.Exists(projectPath))
    {
      throw new FileNotFoundException($"Project file was not found for {projectDirectoryName}.", projectPath);
    }

    return projectPath;
  }

  private static async Task<bool> IsServerReadyAsync(Uri baseUrl, TimeSpan timeout)
  {
    try
    {
      using var handler = new HttpClientHandler
      {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
      };

      using var httpClient = new HttpClient(handler)
      {
        Timeout = timeout,
        BaseAddress = baseUrl,
      };

      var response = await httpClient.GetAsync("/health");
      return response.IsSuccessStatusCode;
    }
    catch
    {
      return false;
    }
  }

  private static async Task PersistProcessLog(
    UiTestOptions options,
    string processName,
    ConcurrentQueue<string> outputLog)
  {
    if (outputLog.IsEmpty)
    {
      return;
    }

    Directory.CreateDirectory(options.ScreenshotDirectory.FullName);
    var logPath = Path.Combine(options.ScreenshotDirectory.FullName, $"{processName}-startup.log");
    await File.WriteAllTextAsync(logPath, FormatServerLog(outputLog));
  }

  private static UiTestProcessSession StartDotNetProcess(
    string processName,
    string projectPath,
    string launchProfile,
    ConcurrentQueue<string> outputLog,
    IReadOnlyList<string> argumentsAfterDoubleDash,
    IReadOnlyDictionary<string, string>? environmentVariables)
  {
    var projectDirectory = Path.GetDirectoryName(projectPath);
    if (string.IsNullOrWhiteSpace(projectDirectory))
    {
      throw new InvalidOperationException($"Unable to determine working directory for {processName} startup.");
    }

    var processArguments = $"run --project \"{projectPath}\" --launch-profile {launchProfile}";
    if (argumentsAfterDoubleDash.Count > 0)
    {
      var appArguments = string.Join(" ", argumentsAfterDoubleDash.Select(argument => argument.Contains(' ') ? $"\"{argument}\"" : argument));
      processArguments = $"{processArguments} -- {appArguments}";
    }

    var processStartInfo = new ProcessStartInfo("dotnet", processArguments)
    {
      WorkingDirectory = projectDirectory,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };

    if (environmentVariables is not null)
    {
      foreach (var (key, value) in environmentVariables)
      {
        processStartInfo.Environment[key] = value;
      }
    }

    var process = new Process { StartInfo = processStartInfo };
    process.OutputDataReceived += (_, eventArgs) =>
    {
      if (!string.IsNullOrEmpty(eventArgs.Data))
      {
        outputLog.Enqueue(eventArgs.Data);
      }
    };

    process.ErrorDataReceived += (_, eventArgs) =>
    {
      if (!string.IsNullOrEmpty(eventArgs.Data))
      {
        outputLog.Enqueue(eventArgs.Data);
      }
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    return new UiTestProcessSession(processName, process, outputLog);
  }

  private static async Task StopProcesses(
    IReadOnlyList<UiTestProcessSession> processSessions,
    UiTestOptions options,
    bool persistLogs)
  {
    for (var i = processSessions.Count - 1; i >= 0; i--)
    {
      var processSession = processSessions[i];
      if (persistLogs)
      {
        await PersistProcessLog(options, processSession.Name, processSession.OutputLog);
      }

      if (processSession.Process.HasExited)
      {
        continue;
      }

      try
      {
        processSession.Process.Kill(true);
        await processSession.Process.WaitForExitAsync();
      }
      catch
      {
      }
    }
  }

  private static async Task WaitForServerReadyAsync(
    UiTestOptions options,
    TimeSpan timeout,
    UiTestProcessSession serverSession)
  {
    var baseUrl = options.BaseUrl;
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
      if (serverSession.Process.HasExited)
      {
        await PersistProcessLog(options, serverSession.Name, serverSession.OutputLog);

        throw new InvalidOperationException(
          $"Server process exited before becoming ready. ExitCode={serverSession.Process.ExitCode}{Environment.NewLine}{FormatServerLog(serverSession.OutputLog)}");
      }

      if (await IsServerReadyAsync(baseUrl, TimeSpan.FromSeconds(5)))
      {
        return;
      }

      await Task.Delay(1000);
    }

    await PersistProcessLog(options, serverSession.Name, serverSession.OutputLog);

    throw new InvalidOperationException(
      $"Server at {baseUrl} did not become ready within {timeout.TotalSeconds} seconds.{Environment.NewLine}{FormatServerLog(serverSession.OutputLog)}");
  }
}

internal sealed record UiTestProcessSession(
  string Name,
  Process Process,
  ConcurrentQueue<string> OutputLog);

internal sealed class UiTestDatabaseSession : IAsyncDisposable
{
  private readonly PostgreSqlContainer _container;

  private UiTestDatabaseSession(
    PostgreSqlContainer container,
    string databaseName,
    string host,
    int port,
    string username,
    string password)
  {
    _container = container;
    DatabaseName = databaseName;
    Host = host;
    Port = port;
    Username = username;
    Password = password;
  }

  public string DatabaseName { get; }

  public string Host { get; }

  public string Password { get; }

  public int Port { get; }

  public string Username { get; }

  public static async Task<UiTestDatabaseSession> Create()
  {
    var databaseName = $"controlr_ui_{Guid.NewGuid():N}";
    var container = new PostgreSqlBuilder("postgres:18-alpine")
      .WithUsername("postgres")
      .WithPassword("password")
      .WithDatabase(databaseName)
      .WithCleanUp(true)
      .Build();

    await container.StartAsync();

    return new UiTestDatabaseSession(
      container,
      databaseName,
      container.Hostname,
      container.GetMappedPublicPort(5432),
      "postgres",
      "password");
  }

  public async ValueTask DisposeAsync()
  {
    await _container.DisposeAsync();
  }
}