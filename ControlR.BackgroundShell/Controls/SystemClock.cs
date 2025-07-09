namespace ControlR.BackgroundShell.Controls;
internal class SystemClock : UserControl
{
  private string _cachedText = string.Empty;
  private Font? _cachedFont = null;
  private SizeF _cachedTextSize;
  private int _lastMinute = -1;
  private int _lastWidth = -1, _lastHeight = -1;

  protected override void OnPaint(PaintEventArgs e)
  {
    base.OnPaint(e);

    // Only proceed if the client size is valid
    if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
      return;

    var g = e.Graphics;
    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

    var now = DateTime.Now;
    var minuteChanged = now.Minute != _lastMinute;
    var sizeChanged = ClientSize.Width != _lastWidth || ClientSize.Height != _lastHeight;

    if (minuteChanged || sizeChanged || _cachedFont == null)
    {
      _lastMinute = now.Minute;
      _lastWidth = ClientSize.Width;
      _lastHeight = ClientSize.Height;
      _cachedText = $"{now:t}\n{now:d}";
      _cachedFont?.Dispose();
      // Find the largest font size that fits the control
      float fontSize = 8f;
      SizeF textSize;
      var availableWidth = ClientSize.Width * 0.60f;
      var availableHeight = ClientSize.Height * 0.60f;
      Font? font = null;
      using (var testFont = new Font(Font.FontFamily, fontSize, FontStyle.Bold))
      {
        do
        {
          font?.Dispose();
          font = new Font(Font.FontFamily, fontSize, FontStyle.Bold);
          textSize = g.MeasureString(_cachedText, font);
          fontSize += 1f;
        } while (textSize.Width < availableWidth && textSize.Height < availableHeight);
      }
      font?.Dispose();
      fontSize -= 1f;
      _cachedFont = new Font(Font.FontFamily, fontSize, FontStyle.Bold);
      _cachedTextSize = g.MeasureString(_cachedText, _cachedFont);
    }

    // Draw time and date with extra line spacing
    var lines = _cachedText.Split('\n');
    if (lines.Length != 2)
      return;
    using var brush = new SolidBrush(ForeColor);
    using var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

    // Measure each line
    var timeSize = g.MeasureString(lines[0], _cachedFont);
    var dateSize = g.MeasureString(lines[1], _cachedFont);
    float extraSpacing = _cachedFont.Height * 0.10f; // 10% extra spacing
    float totalHeight = timeSize.Height + dateSize.Height + extraSpacing;
    float yStart = (ClientSize.Height - totalHeight) / 2f;
    float x = ClientSize.Width;

    // Draw time (top line)
    g.DrawString(lines[0], _cachedFont, brush, new RectangleF(0, yStart, ClientSize.Width, timeSize.Height), sf);
    // Draw date (bottom line, with extra spacing)
    g.DrawString(lines[1], _cachedFont, brush, new RectangleF(0, yStart + timeSize.Height + extraSpacing, ClientSize.Width, dateSize.Height), sf);
  }
}
