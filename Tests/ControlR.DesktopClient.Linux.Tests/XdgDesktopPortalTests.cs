using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using Tmds.DBus.Protocol;
using ControlR.DesktopClient.Linux.XdgPortal;
using Moq;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.DesktopClient.Common.Options;

namespace ControlR.DesktopClient.Linux.Tests;

public class XdgDesktopPortalTests
{
  private readonly XdgDesktopPortal _desktopPortal;
  private readonly Mock<IFileSystem> _fileSystem;
  private readonly ILogger<XdgDesktopPortal> _logger;
  private readonly OptionsMonitorWrapper<DesktopClientOptions> _options;

  public XdgDesktopPortalTests(ITestOutputHelper output)
  {
    var loggerFactory = LoggerFactory.Create(builder =>
    {
      builder.AddProvider(new XunitLoggerProvider(output));
      builder.SetMinimumLevel(LogLevel.Debug);
    });
    _logger = loggerFactory.CreateLogger<XdgDesktopPortal>();
    _fileSystem = new Mock<IFileSystem>();
    _options = new OptionsMonitorWrapper<DesktopClientOptions>(new DesktopClientOptions());
    _desktopPortal = new XdgDesktopPortal(_fileSystem.Object, _options, _logger);
  }

  [WaylandOnlyFact]
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


  [WaylandOnlyFact]
  public async Task CanConnectToDBus()
  {
    await _desktopPortal.Initialize();
    var sessionHandle = await _desktopPortal.GetRemoteDesktopSessionHandle();
    Assert.NotNull(sessionHandle);
  }

  public async Task CanGetPipeWireConnection()
  {
    var connection = await _desktopPortal.GetPipeWireConnection();
    Assert.NotNull(connection);
    Assert.False(connection.Value.Fd.IsInvalid);
    Assert.False(string.IsNullOrEmpty(connection.Value.SessionHandle));
  }
}
