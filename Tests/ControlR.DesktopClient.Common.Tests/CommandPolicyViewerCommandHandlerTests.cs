using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.DesktopClient.Common.Tests;

public class CommandPolicyViewerCommandHandlerTests
{
  [Fact]
  public async Task Handle_AllowedType_ForwardsToInnerHandler()
  {
    var sessionId = Guid.NewGuid();
    var inner = new RecordingHandler();
    var sut = CreateSut(
      inner,
      sessionId,
      RemoteControlCommandPolicyDto.Create(sessionId, [RemoteControlViewerDtoType.KeyEvent]));

    var result = await sut.Handle(CreateWrapper(RemoteControlViewerDtoType.KeyEvent));

    Assert.True(result.IsSuccess);
    Assert.Equal([RemoteControlViewerDtoType.KeyEvent], inner.HandledTypes);
  }

  [Fact]
  public async Task Handle_BrokenSeal_DropsAllCommands()
  {
    var sessionId = Guid.NewGuid();
    var inner = new RecordingHandler();
    var tampered = RemoteControlCommandPolicyDto.Create(
      sessionId,
      [RemoteControlViewerDtoType.KeyEvent]) with
    {
      AllowedDtoTypes =
      [
        RemoteControlViewerDtoType.KeyEvent,
        RemoteControlViewerDtoType.ToggleBlockInput,
      ],
    };

    var sut = CreateSut(inner, sessionId, tampered);

    await sut.Handle(CreateWrapper(RemoteControlViewerDtoType.KeyEvent));

    Assert.Empty(inner.HandledTypes);
  }

  [Fact]
  public async Task Handle_NoPolicy_ForwardsEverything()
  {
    var sessionId = Guid.NewGuid();
    var inner = new RecordingHandler();
    var sut = CreateSut(inner, sessionId, null);

    await sut.Handle(CreateWrapper(RemoteControlViewerDtoType.KeyEvent));
    await sut.Handle(CreateWrapper(RemoteControlViewerDtoType.TogglePrivacyScreen));

    Assert.Equal(
      [RemoteControlViewerDtoType.KeyEvent, RemoteControlViewerDtoType.TogglePrivacyScreen],
      inner.HandledTypes);
  }

  [Fact]
  public async Task Handle_PolicyForDifferentSession_DropsAllCommands()
  {
    var inner = new RecordingHandler();
    var foreignPolicy = RemoteControlCommandPolicyDto.Create(
      Guid.NewGuid(),
      [RemoteControlViewerDtoType.KeyEvent]);

    var sut = CreateSut(inner, Guid.NewGuid(), foreignPolicy);

    await sut.Handle(CreateWrapper(RemoteControlViewerDtoType.KeyEvent));

    Assert.Empty(inner.HandledTypes);
  }

  [Fact]
  public async Task Handle_TypeNotAllowedByPolicy_IsDroppedWithoutReachingInner()
  {
    var sessionId = Guid.NewGuid();
    var inner = new RecordingHandler();
    var sut = CreateSut(
      inner,
      sessionId,
      RemoteControlCommandPolicyDto.Create(sessionId, [RemoteControlViewerDtoType.KeyEvent]));

    var result = await sut.Handle(CreateWrapper(RemoteControlViewerDtoType.TypeText));

    Assert.True(result.IsSuccess);
    Assert.Empty(inner.HandledTypes);
  }

  private static CommandPolicyViewerCommandHandler CreateSut(
    IRemoteControlViewerCommandHandler inner,
    Guid sessionId,
    RemoteControlCommandPolicyDto? policy) =>
    new(
      inner,
      Microsoft.Extensions.Options.Options.Create(
        new RemoteControlSessionOptions { SessionId = sessionId, CommandPolicy = policy }),
      NullLogger<CommandPolicyViewerCommandHandler>.Instance);

  private static RemoteControlViewerWrapper CreateWrapper(RemoteControlViewerDtoType dtoType) =>
    RemoteControlViewerWrapper.Create("payload", dtoType);

  private sealed class RecordingHandler : IRemoteControlViewerCommandHandler
  {
    public List<RemoteControlViewerDtoType> HandledTypes { get; } = [];

    public Task<Result> Handle(RemoteControlViewerWrapper wrapper)
    {
      HandledTypes.Add(wrapper.DtoType);
      return Task.FromResult(Result.Ok());
    }
  }
}
