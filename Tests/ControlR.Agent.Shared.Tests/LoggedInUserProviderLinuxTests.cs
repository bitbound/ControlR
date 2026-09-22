using ControlR.Agent.Shared.Services.Linux;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Shared.Tests;

/// <summary>
/// Pins that the provider returns each logged-in user with the facts callers need, without
/// deciding launch-vs-stop policy itself (#157).
/// </summary>
public class LoggedInUserProviderLinuxTests
{
  [Fact]
  public async Task GetLoggedInUsers_ExcludesClosingSessions()
  {
    var provider = CreateProvider(
      ("2", Session("2", "1000", "wayland", "closing", "yes")));

    var users = await provider.GetLoggedInUsers();

    Assert.Empty(users);
  }

  [Fact]
  public async Task GetLoggedInUsers_ExcludesSystemUsers()
  {
    var provider = CreateProvider(
      ("2", Session("2", "999", "wayland", "active", "yes")));

    var users = await provider.GetLoggedInUsers();

    Assert.Empty(users);
  }

  [Fact]
  public async Task GetLoggedInUsers_ExcludesTextSessions()
  {
    var provider = CreateProvider(
      ("2", Session("2", "1000", "tty", "active", "yes")));

    var users = await provider.GetLoggedInUsers();

    Assert.Empty(users);
  }

  [Fact]
  public async Task GetLoggedInUsers_WhenGraphicalSessionIsActive_ReturnsUserAsActive()
  {
    var provider = CreateProvider(
      ("2", Session("2", "1000", "wayland", "active", "yes")));

    var users = await provider.GetLoggedInUsers();

    var user = Assert.Single(users);
    Assert.Equal("1000", user.Uid);
    Assert.True(user.IsActive);
  }

  [Fact]
  public async Task GetLoggedInUsers_WhenGraphicalSessionIsInactive_StillReturnsUserAsInactive()
  {
    var provider = CreateProvider(
      ("2", Session("2", "1000", "wayland", "active", "no")));

    var users = await provider.GetLoggedInUsers();

    var user = Assert.Single(users);
    Assert.Equal("1000", user.Uid);
    Assert.False(user.IsActive);
    Assert.True(user.IsGraphical);
  }

  [Fact]
  public async Task GetLoggedInUsers_WhenUserHasActiveAndInactiveSessions_ReturnsUserOnceAsActive()
  {
    var provider = CreateProvider(
      ("2", Session("2", "1000", "wayland", "active", "no")),
      ("3", Session("3", "1000", "x11", "active", "yes")));

    var users = await provider.GetLoggedInUsers();

    var user = Assert.Single(users);
    Assert.Equal("1000", user.Uid);
    Assert.True(user.IsActive);
  }

  private static ILoggedInUserProvider CreateProvider(params (string SessionId, string Info)[] sessions)
  {
    var processManager = new Mock<IProcessManager>();
    var list = string.Join("\n", sessions.Select(s => $"{s.SessionId} 1000 user seat0")) + "\n";

    var sequence = processManager.SetupSequence(
      x => x.GetProcessOutput(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()));
    sequence.ReturnsAsync(Result.Ok(list));
    foreach (var (_, info) in sessions)
    {
      sequence.ReturnsAsync(Result.Ok(info));
    }

    return new LoggedInUserProviderLinux(
      processManager.Object,
      NullLogger<LoggedInUserProviderLinux>.Instance);
  }

  private static string Session(string id, string user, string type, string state, string active)
  {
    return $"Id={id}\nUser={user}\nType={type}\nState={state}\nActive={active}\n";
  }
}
