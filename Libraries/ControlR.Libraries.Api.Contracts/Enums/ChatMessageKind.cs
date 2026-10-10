namespace ControlR.Libraries.Api.Contracts.Enums;

/// <summary>
/// What a chat message means, as opposed to who sent it. Peers that predate this member send no kind at all,
/// so <see cref="TextSend"/> must stay the zero value to keep their payloads reading as plain text.
/// </summary>
public enum ChatMessageKind
{
  TextSend = 0,
  WindowClose = 1,
}
