using ControlR.Libraries.Shared.Enums;

namespace ControlR.Streamer.Messages;

public record CursorChangedMessage(WindowsCursor Cursor);