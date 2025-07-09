using System.ComponentModel;

namespace ControlR.BackgroundShell.Controls;

internal class TaskbarButton : Button
{
  public TaskbarButton()
  {
    FlatStyle = FlatStyle.Flat;
    FlatAppearance.BorderSize = 0;
    BackColor = Color.Transparent;
    ForeColor = Color.White;
    Size = new Size(50, 50);
    Font = new Font("Microsoft Sans Serif", 16F, FontStyle.Regular);
  }

  protected void DrawOpenIndicator(PaintEventArgs pevent, bool isFocused)
  {
    var graphics = pevent.Graphics;
    int barWidth = isFocused ? Width / 3 : Width / 4;
    int barHeight = 4;
    int barX = (Width - barWidth) / 2;
    int barY = Height - barHeight - 2;

    var barColor = isFocused ? Color.DodgerBlue : Color.LightGray;
    using var brush = new SolidBrush(barColor);
    graphics.FillRectangle(brush, barX, barY, barWidth, barHeight);
  }

  protected override void OnMouseEnter(EventArgs e)
  {
    base.OnMouseHover(e);
    BackColor = Color.DarkCyan;
    ForeColor = Color.Black;
  }

  protected override void OnMouseLeave(EventArgs e)
  {
    base.OnMouseLeave(e);
    BackColor = Color.Transparent;
    ForeColor = Color.White;
  }

     protected override bool ShowFocusCues
    {
        get
        {
            return false;
        }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }
}
