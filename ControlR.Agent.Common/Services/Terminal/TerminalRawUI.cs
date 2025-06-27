using System.Management.Automation.Host;

namespace ControlR.Agent.Common.Services.Terminal;

// Minimal RawUI implementation
internal class TerminalRawUI : PSHostRawUserInterface
{
  public override ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;
  public override ConsoleColor ForegroundColor { get; set; } = ConsoleColor.White;
  public override Coordinates CursorPosition { get; set; }
  public override int CursorSize { get; set; } = 25;
  public override Size BufferSize { get; set; } = new(120, 30);
  public override Size MaxPhysicalWindowSize => new(120, 30);
  public override Size MaxWindowSize => new(120, 30);
  public override Coordinates WindowPosition { get; set; }
  public override Size WindowSize { get; set; } = new(120, 30);
  public override string WindowTitle { get; set; } = "ControlR Terminal";
  public override bool KeyAvailable => false;

  public override void FlushInputBuffer() { }
  public override BufferCell[,] GetBufferContents(Rectangle rectangle) => new BufferCell[rectangle.Bottom - rectangle.Top + 1, rectangle.Right - rectangle.Left + 1];
  public override KeyInfo ReadKey(ReadKeyOptions options) => new();
  public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill) { }
  public override void SetBufferContents(Coordinates origin, BufferCell[,] contents) { }
  public override void SetBufferContents(Rectangle rectangle, BufferCell fill) { }
}
