using System.Net.WebSockets;
using System.Text.RegularExpressions;
using ControlR.Libraries.TestingUtilities;
using ControlR.Libraries.TestingUtilities.Extensions;
using ControlR.Libraries.WebSocketRelay.Client;
using ControlR.Libraries.WebSocketRelay.Common.Extensions;
using ControlR.Libraries.WebSocketRelay.Common.Options;
using ControlR.Libraries.WebSocketRelay.Common.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ControlR.Libraries.WebSocketRelay.Tests;

// How long a peer waits for its partner to arrive in the query string, so the server has to own the
// ceiling.  A wait the client picks for itself can hold a session slot open for as long as it likes.
public class RelayTimeoutTests
{
  private static readonly TimeSpan _clockTick = TimeSpan.FromMilliseconds(1);
  private static readonly TimeSpan _handshakeBudget = TimeSpan.FromSeconds(10);

  private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

  [Fact]
  public async Task MaximumWaitForPartnerTimeout_NonPositive_IsRefusedAtStartup()
  {
    await Assert.ThrowsAsync<OptionsValidationException>(
      () => StartRelayAsync(options => options.MaxWaitForPartnerTimeout = TimeSpan.Zero));
  }

  [Fact]
  public async Task WaitForPartner_AfterTimeout_EvictsTheSession()
  {
    using var app = await StartRelayAsync();
    var sessionId = Guid.NewGuid();
    var store = app.Services.GetRequiredService<IRelaySessionStore>();

    var status = await RefusedHandshakeStatusAsync(
      app.GetTestServer(),
      RelayUri(sessionId, RelayRole.Requester, 1),
      waitForPartner: TimeSpan.FromSeconds(1));

    Assert.Equal(StatusCodes.Status408RequestTimeout, status);
    Assert.Equal(0, store.Count);
  }

  [Fact]
  public async Task WaitForPartner_AfterTimeout_FreshTokenPairsOnTheSameSessionId()
  {
    using var app = await StartRelayAsync();
    var server = app.GetTestServer();
    var sessionId = Guid.NewGuid();

    var status = await RefusedHandshakeStatusAsync(
      server,
      RelayUri(sessionId, RelayRole.Requester, 1, "first-token"),
      waitForPartner: TimeSpan.FromSeconds(1));
    Assert.Equal(StatusCodes.Status408RequestTimeout, status);

    var (requester, responder) = await PairAsync(
      server,
      RelayUri(sessionId, RelayRole.Requester, 30, "second-token"),
      RelayUri(sessionId, RelayRole.Responder, 30, "second-token"));

    requester.Dispose();
    responder.Dispose();
  }

  [Fact]
  public async Task WaitForPartner_AfterTimeout_RoleSlotIsFreeForTheSameToken()
  {
    using var app = await StartRelayAsync();
    var server = app.GetTestServer();
    var sessionId = Guid.NewGuid();

    var status = await RefusedHandshakeStatusAsync(
      server,
      RelayUri(sessionId, RelayRole.Requester, 1),
      waitForPartner: TimeSpan.FromSeconds(1));
    Assert.Equal(StatusCodes.Status408RequestTimeout, status);

    var (requester, responder) = await PairAsync(
      server,
      RelayUri(sessionId, RelayRole.Requester, 30),
      RelayUri(sessionId, RelayRole.Responder, 30));

    requester.Dispose();
    responder.Dispose();
  }

  [Fact]
  public async Task WaitForPartner_BothPeers_PairAndStream()
  {
    using var app = await StartRelayAsync();
    var server = app.GetTestServer();
    var sessionId = Guid.NewGuid();

    // Neither handshake completes before both peers arrive, so both connects have to be in flight.
    var pairingRequester = server
      .CreateWebSocketClient()
      .ConnectAsync(RelayUri(sessionId, RelayRole.Requester, 30), TestContext.Current.CancellationToken);
    var pairingResponder = server
      .CreateWebSocketClient()
      .ConnectAsync(RelayUri(sessionId, RelayRole.Responder, 30), TestContext.Current.CancellationToken);

    await Task.WhenAll(pairingRequester, pairingResponder);

    var requesterSocket = await pairingRequester;
    var responderSocket = await pairingResponder;

    await requesterSocket.SendAsync(
      "hello"u8.ToArray(), WebSocketMessageType.Binary, true, TestContext.Current.CancellationToken);

    var buffer = new byte[64];
    var received = await responderSocket.ReceiveAsync(buffer, TestContext.Current.CancellationToken);

    Assert.Equal(WebSocketMessageType.Binary, received.MessageType);
    Assert.Equal("hello"u8.ToArray(), buffer.AsSpan(0, received.Count).ToArray());
  }

  [Fact]
  public async Task WaitForPartner_TimeoutAboveServerMaximum_IsRefusedAtTheMaximum()
  {
    using var app = await StartRelayAsync(options => options.MaxWaitForPartnerTimeout = TimeSpan.FromSeconds(1));

    var status = await RefusedHandshakeStatusAsync(
      app.GetTestServer(),
      RelayUri(timeoutSeconds: 3_600),
      waitForPartner: TimeSpan.FromSeconds(1));

    Assert.Equal(StatusCodes.Status408RequestTimeout, status);
  }

  [Fact]
  public async Task WaitForPartner_TimeoutAboveTheIntegerRange_IsCappedByTheMaximum()
  {
    using var app = await StartRelayAsync(options => options.MaxWaitForPartnerTimeout = TimeSpan.FromSeconds(1));

    var status = await RefusedHandshakeStatusAsync(
      app.GetTestServer(),
      RelayUri(timeoutSeconds: 4_000_000_000),
      waitForPartner: TimeSpan.FromSeconds(1));

    Assert.Equal(StatusCodes.Status408RequestTimeout, status);
  }

  [Fact]
  public async Task WaitForPartner_TimeoutBelowServerMaximum_IsRefusedAtTheRequestedTime()
  {
    using var app = await StartRelayAsync();

    var status = await RefusedHandshakeStatusAsync(
      app.GetTestServer(),
      RelayUri(timeoutSeconds: 2),
      waitForPartner: TimeSpan.FromSeconds(2));

    Assert.Equal(StatusCodes.Status408RequestTimeout, status);
  }

  [Fact]
  public async Task WaitForPartner_TimeoutOmitted_IsCappedByTheMaximum()
  {
    using var app = await StartRelayAsync(options => options.MaxWaitForPartnerTimeout = TimeSpan.FromSeconds(1));

    var status = await RefusedHandshakeStatusAsync(
      app.GetTestServer(),
      RelayUriWithoutTimeout(),
      waitForPartner: TimeSpan.FromSeconds(1));

    Assert.Equal(StatusCodes.Status408RequestTimeout, status);
  }

  [Fact]
  public async Task WaitForPartner_ZeroTimeout_IsRefusedInsteadOfHeldOpen()
  {
    using var app = await StartRelayAsync();

    var status = await RefusedHandshakeStatusAsync(app.GetTestServer(), RelayUri(timeoutSeconds: 0));

    Assert.Equal(StatusCodes.Status400BadRequest, status);
  }

  private static Uri RelayUri(
    uint timeoutSeconds,
    Guid? sessionId = null,
    RelayRole role = RelayRole.Requester) =>
    RelayUri(sessionId ?? Guid.NewGuid(), role, timeoutSeconds);

  private static Uri RelayUri(
    Guid sessionId,
    RelayRole role,
    uint timeoutSeconds,
    string accessToken = "test-access-token") =>
    RelayUriBuilder.Build(
      new Uri("ws://localhost"),
      "/relay",
      sessionId,
      accessToken,
      role,
      timeoutSeconds);

  // The middleware falls back to a wait that was never asked for, so this omits the value entirely.
  private static Uri RelayUriWithoutTimeout(Guid? sessionId = null) =>
    new(
      $"ws://localhost/relay?sessionId={sessionId ?? Guid.NewGuid()}&" +
      "accessToken=test-access-token&" +
      "role=requester");

  // Both connects have to be in flight before either handshake completes, and a refused one faults
  // without the other ever finishing, so the pair is raced against the same deadline.
  private async Task<(WebSocket Requester, WebSocket Responder)> PairAsync(
    TestServer server,
    Uri requesterUri,
    Uri responderUri)
  {
    var requester = server
      .CreateWebSocketClient()
      .ConnectAsync(requesterUri, TestContext.Current.CancellationToken);
    var responder = server
      .CreateWebSocketClient()
      .ConnectAsync(responderUri, TestContext.Current.CancellationToken);
    var both = Task.WhenAll(requester, responder);

    var answered = await Task.WhenAny(both, Task.Delay(_handshakeBudget, TestContext.Current.CancellationToken));
    Assert.True(
      ReferenceEquals(answered, both),
      $"The peers did not pair within {_handshakeBudget}.  " +
      $"Requester: '{requesterUri}'.  Responder: '{responderUri}'.");

    var sockets = await both;
    return (sockets[0], sockets[1]);
  }

  // The test server reports a refused handshake only through the exception it throws, so the status
  // code has to come out of the message.  Its client also ignores the cancellation token it is
  // handed, so a wait that never expires has to be raced against a deadline instead of cancelled.
  //
  // A wait the server takes from its own clock is advanced rather than waited on, which pins the
  // refusal to the instant the wait expires instead of to the status code alone.
  private async Task<int> RefusedHandshakeStatusAsync(
    TestServer server,
    Uri uri,
    TimeSpan? waitForPartner = null)
  {
    var socket = server.CreateWebSocketClient();
    var connecting = socket.ConnectAsync(uri, TestContext.Current.CancellationToken);

    if (waitForPartner is { } wait)
    {
      var armed = await _timeProvider.WaitForWaiters(
        x => x > 0,
        cancellationToken: TestContext.Current.CancellationToken);
      Assert.True(armed, $"The server never armed its wait-for-partner timer for '{uri}'.");

      // One tick short of the wait, and the handshake is still open.
      _timeProvider.Advance(wait - _clockTick);
      Assert.False(
        connecting.IsCompleted,
        $"The server refused the handshake for '{uri}' before the wait elapsed.");

      _timeProvider.Advance(_clockTick);
    }

    var answered = await Task.WhenAny(connecting, Task.Delay(_handshakeBudget));
    Assert.True(
      ReferenceEquals(answered, connecting),
      $"The server never answered the handshake for '{uri}'.  It is still waiting for a partner.");

    var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => connecting);

    var match = Regex.Match(failure.Message, @"status code: (\d{3})");
    Assert.True(match.Success, $"Handshake was refused without a status code.  {failure.Message}");

    return int.Parse(match.Groups[1].Value);
  }

  private async Task<WebApplication> StartRelayAsync(Action<WebSocketRelayOptions>? configureOptions = null)
  {
    var builder = WebApplication.CreateSlimBuilder();
    builder.WebHost.UseTestServer();
    builder.Services.AddAuthorization();
    builder.Services.AddWebSocketRelay(configureOptions ?? (_ => { }));
    builder.Services.ReplaceSingleton<TimeProvider, FakeTimeProvider>(_timeProvider);

    var app = builder.Build();
    app.MapWebSocketRelay();
    await app.StartAsync(TestContext.Current.CancellationToken);

    return app;
  }
}
