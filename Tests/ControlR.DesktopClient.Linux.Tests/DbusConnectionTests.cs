using ControlR.Libraries.NativeInterop.Unix.Linux;
using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace ControlR.DesktopClient.Linux.Tests;

public class DbusConnectionTests
{
  private readonly ILogger _logger;
  private readonly ITestOutputHelper _output;

  public DbusConnectionTests(ITestOutputHelper output)
  {
    _output = output;
    var loggerFactory = LoggerFactory.Create(builder =>
    {
      builder.AddProvider(new XunitLoggerProvider(output));
      builder.SetMinimumLevel(LogLevel.Debug);
    });
    _logger = loggerFactory.CreateLogger<DbusConnectionTests>();
  }

  [WaylandOnlyFact]
  public async Task CanConnectToSessionBus()
  {
    var address = Address.Session;
    _output.WriteLine($"Session bus address: {address}");
    Assert.False(string.IsNullOrEmpty(address));

    var connection = new Connection(address);
    await connection.ConnectAsync();

    _output.WriteLine($"Connected as: {connection.UniqueName}");
    Assert.NotNull(connection.UniqueName);
    Assert.StartsWith(":", connection.UniqueName);

    connection.Dispose();
  }

  [WaylandOnlyFact]
  public async Task CanCallPortalPropertyGet()
  {
    var address = Address.Session!;
    var connection = new Connection(address);
    await connection.ConnectAsync();

    _output.WriteLine($"Connected as: {connection.UniqueName}");

    // Try to get a property from the portal (this shouldn't require any user interaction)
    MessageBuffer message;
    using (var writer = connection.GetMessageWriter())
    {
      writer.WriteMethodCallHeader(
        destination: "org.freedesktop.portal.Desktop",
        path: "/org/freedesktop/portal/desktop",
        @interface: "org.freedesktop.DBus.Properties",
        signature: "ss",
        member: "Get");
      writer.WriteString("org.freedesktop.portal.ScreenCast");
      writer.WriteString("version");
      message = writer.CreateMessage();
    }

    try
    {
      var result = await connection.CallMethodAsync(message, (Message m, object? s) =>
      {
        var r = m.GetBodyReader();
        var variant = r.ReadVariantValue();
        return variant.GetUInt32();
      });

      _output.WriteLine($"ScreenCast portal version: {result}");
      Assert.True(result > 0);
    }
    catch (Exception ex)
    {
      _output.WriteLine($"Error calling Get property: {ex.GetType().Name}: {ex.Message}");
      throw;
    }
    finally
    {
      connection.Dispose();
    }
  }

  [WaylandOnlyFact]
  public async Task CanCallCreateSessionDirectly()
  {
    var address = Address.Session!;
    var connection = new Connection(address);
    await connection.ConnectAsync();

    _output.WriteLine($"Connected as: {connection.UniqueName}");

    var sessionToken = "test_session_" + Guid.NewGuid().ToString("N");
    var handleToken = "test_handle_" + Guid.NewGuid().ToString("N");

    MessageBuffer message;
    using (var writer = connection.GetMessageWriter())
    {
      writer.WriteMethodCallHeader(
        destination: "org.freedesktop.portal.Desktop",
        path: "/org/freedesktop/portal/desktop",
        @interface: "org.freedesktop.portal.ScreenCast",
        signature: "a{sv}",
        member: "CreateSession");

      // Write options dictionary
      var dictStart = writer.WriteDictionaryStart();
      writer.WriteString("handle_token");
      writer.WriteVariant(VariantValue.String(handleToken));
      writer.WriteString("session_handle_token");
      writer.WriteVariant(VariantValue.String(sessionToken));
      writer.WriteDictionaryEnd(dictStart);

      message = writer.CreateMessage();
    }

    try
    {
      var requestPath = await connection.CallMethodAsync(message, (Message m, object? s) =>
      {
        var r = m.GetBodyReader();
        return r.ReadObjectPath().ToString();
      });

      _output.WriteLine($"Got request path: {requestPath}");
      Assert.NotNull(requestPath);
      Assert.Contains("request", requestPath);
    }
    catch (Exception ex)
    {
      _output.WriteLine($"Error calling CreateSession: {ex.GetType().Name}: {ex.Message}");
      _output.WriteLine($"Stack trace: {ex.StackTrace}");
      throw;
    }
    finally
    {
      connection.Dispose();
    }
  }
}
