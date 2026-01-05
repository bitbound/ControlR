using System.Collections.Concurrent;

namespace ControlR.Libraries.Viewer.Common.State;

public interface ITerminalState
{
  string CommandInputText { get; set; }
  bool EnableMultiline { get; set; }
  Guid Id { get; }
  ConcurrentList<string> InputHistory { get; }
  int InputHistoryIndex { get; set; }
  string? LastCompletionInput { get; set; }
  int LastCursorIndex { get; set; }
  ConcurrentQueue<TerminalOutputDto> Output { get; }
}

public class TerminalState : ITerminalState
{
  public string CommandInputText { get; set; } = string.Empty;
  public bool EnableMultiline { get; set; }
  public Guid Id { get; } = Guid.NewGuid();
  public ConcurrentList<string> InputHistory { get; } = [];
  public int InputHistoryIndex { get; set; }
  public string? LastCompletionInput { get; set; }
  public int LastCursorIndex { get; set; }
  public ConcurrentQueue<TerminalOutputDto> Output { get; } = [];
}