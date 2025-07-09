using ControlR.BackgroundShell.Helpers;

namespace ControlR.BackgroundShell.Controls;

internal class TaskbarFormButton : TaskbarButton
{
  public TaskbarFormButton()
  {
    BackgroundImageLayout = ImageLayout.Center;
  }

  public Func<Form?>? GetFormFunc { get; set; }

  protected override void OnPaint(PaintEventArgs pevent)
  {
    base.OnPaint(pevent);

    if (GetFormFunc?.Invoke() is not { } form)
    {
      return;
    }

    if (form.IsDisposed)
    {
      return;
    }

    var focused = Win32Interop.IsWindowFocused(form.Handle);
    DrawOpenIndicator(pevent, focused);
  }
}