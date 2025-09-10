using System.Collections.Concurrent;

namespace ControlR.Web.Client.Services.DeviceAccess;

public interface ITerminalState
{
  Guid Id { get; }
  string CommandInputText { get; set; }
  ConcurrentList<string> InputHistory { get; }
  bool EnableMultiline { get; set; }
  int InputHistoryIndex { get; set; }
  string? LastCompletionInput { get; set; }
  int LastCursorIndex { get; set; }
  ConcurrentQueue<TerminalOutputDto> Output { get; }
}

public class TerminalState : ITerminalState
{
  public Guid Id { get; } = Guid.NewGuid();
  public string CommandInputText { get; set; } = string.Empty;
  public ConcurrentList<string> InputHistory { get; } = [];
  public bool EnableMultiline { get; set; }
  public int InputHistoryIndex { get; set; }
  public string? LastCompletionInput { get; set; }
  public int LastCursorIndex { get; set; }
  public ConcurrentQueue<TerminalOutputDto> Output { get; } = [];
}