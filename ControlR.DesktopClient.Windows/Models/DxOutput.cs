using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;

namespace ControlR.DesktopClient.Windows.Models;

internal sealed class DxOutput(
  string deviceName,
  Rectangle bounds,
  IDXGIAdapter1 adapter,
  ID3D11Device device,
  ID3D11DeviceContext deviceContext,
  IDXGIOutputDuplication outputDuplication,
  DXGI_MODE_ROTATION rotation)
  : IDisposable
{
  public IDXGIAdapter1 Adapter { get; } = adapter;
  public Rectangle Bounds { get; } = bounds;
  public ID3D11Device Device { get; } = device;
  public ID3D11DeviceContext DeviceContext { get; } = deviceContext;
  public string DeviceName { get; } = deviceName;
  public bool IsDisposed { get; private set; }

  public Rectangle LastCursorArea { get; set; }
  public DateTimeOffset LastSuccessfulCapture { get; set; }
  public IDXGIOutputDuplication OutputDuplication { get; } = outputDuplication;
  public DXGI_MODE_ROTATION Rotation { get; } = rotation;

  public void Dispose()
  {
    if (IsDisposed)
    {
      return;
    }

    IsDisposed = true;

    try
    {
      OutputDuplication.ReleaseFrame();
    }
    catch
    {
    }

    try
    {
      Marshal.FinalReleaseComObject(OutputDuplication);
      Marshal.FinalReleaseComObject(DeviceContext);
      Marshal.FinalReleaseComObject(Device);
      Marshal.FinalReleaseComObject(Adapter);
    }
    catch
    {
    }

    GC.SuppressFinalize(this);
  }
}