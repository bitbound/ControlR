using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using Tmds.DBus.Protocol;
using ControlR.Libraries.NativeInterop.Unix.Linux.XdgPortal;

namespace ControlR.DesktopClient.Linux.Tests;

public class XdgDesktopPortalTests
{
  private readonly ILogger _logger;

  public XdgDesktopPortalTests(ITestOutputHelper output)
  {
    var loggerFactory = LoggerFactory.Create(builder =>
    {
      builder.AddProvider(new XunitLoggerProvider(output));
      builder.SetMinimumLevel(LogLevel.Debug);
    });
    _logger = loggerFactory.CreateLogger<XdgDesktopPortalTests>();
  }

  [WaylandOnly]
  public async Task CanConnectToDBus()
  {
    using var portal = await XdgDesktopPortal.CreateAsync(_logger);
    Assert.NotNull(portal);
  }

  [WaylandOnly]
  public async Task CanCheckScreenCastAvailability()
  {
    using var portal = await XdgDesktopPortal.CreateAsync(_logger);
    var isAvailable = await portal.IsScreenCastAvailableAsync();
    Assert.True(isAvailable, "ScreenCast portal should be available on Wayland");
  }

  [WaylandOnly]
  public async Task CanCheckRemoteDesktopAvailability()
  {
    using var portal = await XdgDesktopPortal.CreateAsync(_logger);
    var isAvailable = await portal.IsRemoteDesktopAvailableAsync();
    Assert.True(isAvailable, "RemoteDesktop portal should be available on Wayland");
  }

  [WaylandOnly]
  public async Task CanCallPortalDirectly()
  {
    var address = Address.Session;
    Assert.False(string.IsNullOrEmpty(address), "Session bus address should be available");
    
    var connection = new Connection(address);
    await connection.ConnectAsync();
    
    _logger.LogInformation("Connected as {UniqueName}", connection.UniqueName);
    
    var handleToken = "test_handle_123";
    var sessionToken = "test_session_123";
    
    MessageBuffer message;
    using (var writer = connection.GetMessageWriter())
    {
      writer.WriteMethodCallHeader(
        destination: "org.freedesktop.portal.Desktop",
        path: "/org/freedesktop/portal/desktop",
        @interface: "org.freedesktop.portal.ScreenCast",
        signature: "a{sv}",
        member: "CreateSession");
      
      var dictStart = writer.WriteDictionaryStart();
      writer.WriteString("handle_token");
      writer.WriteVariant(VariantValue.String(handleToken));
      writer.WriteString("session_handle_token");
      writer.WriteVariant(VariantValue.String(sessionToken));
      writer.WriteDictionaryEnd(dictStart);
      
      message = writer.CreateMessage();
    }
    
    var requestPath = await connection.CallMethodAsync(message, (Message m, object? s) =>
    {
      var r = m.GetBodyReader();
      return r.ReadObjectPath().ToString();
    });
    
    _logger.LogInformation("Got request path: {Path}", requestPath);
    Assert.NotNull(requestPath);
    Assert.Contains("request", requestPath);
    
    connection.Dispose();
  }

  [WaylandOnly]
  public async Task CanCreateScreenCastSession()
  {
    using var portal = await XdgDesktopPortal.CreateAsync(_logger);
    var result = await portal.CreateScreenCastSessionAsync();
    
    Assert.True(result.IsSuccess, $"Failed to create session: {result.Reason}");
    Assert.NotNull(result.Value);
    Assert.NotEmpty(result.Value);
    _logger.LogInformation("Created session: {Session}", result.Value);
  }

  [WaylandOnly]
  public async Task CanCreateRemoteDesktopSession()
  {
    using var portal = await XdgDesktopPortal.CreateAsync(_logger);
    var result = await portal.CreateRemoteDesktopSessionAsync();
    
    Assert.True(result.IsSuccess, $"Failed to create session: {result.Reason}");
    Assert.NotNull(result.Value);
    Assert.NotEmpty(result.Value);
    _logger.LogInformation("Created session: {Session}", result.Value);
  }

  [WaylandOnly]
  public async Task CanSelectScreenCastSources()
  {
    using var portal = await XdgDesktopPortal.CreateAsync(_logger);
    
    var sessionResult = await portal.CreateScreenCastSessionAsync();
    Assert.True(sessionResult.IsSuccess);
    
    var selectResult = await portal.SelectScreenCastSourcesAsync(
      sessionResult.Value!,
      sourceTypes: 1,
      multipleSources: false,
      cursorMode: 4);
    
    Assert.True(selectResult.IsSuccess, $"Failed to select sources: {selectResult.Reason}");
  }

  [WaylandOnly]
  public async Task CanSelectRemoteDesktopDevices()
  {
    using var portal = await XdgDesktopPortal.CreateAsync(_logger);
    
    var sessionResult = await portal.CreateRemoteDesktopSessionAsync();
    Assert.True(sessionResult.IsSuccess);
    
    var selectResult = await portal.SelectRemoteDesktopDevicesAsync(
      sessionResult.Value!,
      deviceTypes: 3);
    
    Assert.True(selectResult.IsSuccess, $"Failed to select devices: {selectResult.Reason}");
  }
}
