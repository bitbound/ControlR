using ControlR.Libraries.WebSocketRelay.Client;
using ControlR.Libraries.WebSocketRelay.Common.Sessions;

namespace ControlR.Libraries.WebSocketRelay.Tests;

// The pairing wait has to tell a partner that arrives as the wait expires apart from one that never
// arrives.  The middleware evicts the session on the latter so the id can be rebuilt.
public class RelayPairingTests
{
  [Fact]
  public async Task WaitForPartner_AfterTimeout_RefusesTheMissingRole()
  {
    await using var signaler = new SessionSignaler("access-token");
    signaler.TryAssignRole(Guid.NewGuid(), RelayRole.Requester);
    signaler.SignalReady(RelayRole.Requester);

    using var cancelled = new CancellationTokenSource();
    await cancelled.CancelAsync();

    Assert.False(await signaler.WaitForPartner(cancelled.Token));

    // The waiter is gone and will never set its websocket, so a peer admitted here accepts a socket
    // and then blocks in SetWebsocket until its own deadline.
    Assert.False(signaler.SignalReady(RelayRole.Responder));
  }

  [Fact]
  public async Task WaitForPartner_DisposedSignaler_ReportsUnpaired()
  {
    var signaler = new SessionSignaler("access-token");
    signaler.TryAssignRole(Guid.NewGuid(), RelayRole.Requester);
    signaler.SignalReady(RelayRole.Requester);

    await signaler.DisposeAsync();

    using var cancelled = new CancellationTokenSource();
    await cancelled.CancelAsync();

    Assert.False(await signaler.WaitForPartner(cancelled.Token));
  }

  [Fact]
  public async Task WaitForPartner_PartnerArrivesAsTheWaitExpires_ReportsPaired()
  {
    await using var signaler = new SessionSignaler("access-token");
    signaler.TryAssignRole(Guid.NewGuid(), RelayRole.Requester);
    signaler.TryAssignRole(Guid.NewGuid(), RelayRole.Responder);
    signaler.SignalReady(RelayRole.Requester);
    signaler.SignalReady(RelayRole.Responder);

    using var cancelled = new CancellationTokenSource();
    await cancelled.CancelAsync();

    Assert.True(await signaler.WaitForPartner(cancelled.Token));
  }

  [Fact]
  public async Task WaitForPartner_WaitExpiresWithoutAPartner_ReportsUnpaired()
  {
    await using var signaler = new SessionSignaler("access-token");
    signaler.TryAssignRole(Guid.NewGuid(), RelayRole.Requester);
    signaler.SignalReady(RelayRole.Requester);

    using var cancelled = new CancellationTokenSource();
    await cancelled.CancelAsync();

    Assert.False(await signaler.WaitForPartner(cancelled.Token));
  }
}
