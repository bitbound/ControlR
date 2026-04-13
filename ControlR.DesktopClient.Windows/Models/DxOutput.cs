using System.Drawing;
using System.Runtime.InteropServices;
using ControlR.Libraries.Shared.Helpers;
using SkiaSharp;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;

namespace ControlR.DesktopClient.Windows.Models;

/// <summary>
/// Holds state and resources for a single DirectX output (display).
/// Used by <see cref="ScreenGrabberWindows"/> to manage frame duplication.
/// </summary>
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

  private SKBitmap? _lastCapturedSkBitmap;

  /// <summary>
  /// Gets the DXGI adapter that created the device.
  /// </summary>
  public IDXGIAdapter1 Adapter { get; } = adapter;
  /// <summary>
  /// Gets the bounds of this output in screen coordinates.
  /// </summary>
  public Rectangle Bounds { get; } = bounds;
  /// <summary>
  /// Gets the Direct3D 11 device for this output.
  /// </summary>
  public ID3D11Device Device { get; } = device;
  /// <summary>
  /// Gets the Direct3D 11 device context.
  /// </summary>
  public ID3D11DeviceContext DeviceContext { get; } = deviceContext;
  /// <summary>
  /// Gets the device name identifier for this output.
  /// </summary>
  public string DeviceName { get; } = deviceName;
  /// <summary>
  /// Gets whether this output has been disposed.
  /// </summary>
  public bool IsDisposed { get; private set; }
  /// <summary>
  /// Gets the last captured SKBitmap (without cursor) for cursor-only updates.
  /// </summary>
  public SKBitmap? LastCapturedSkBitmap => _lastCapturedSkBitmap;
  /// <summary>
  /// Gets or sets the screen area occupied by the cursor in the last capture.
  /// </summary>
  public Rectangle LastCursorArea { get; set; }
  /// <summary>
  /// Gets or sets the handle of the cursor observed during the last captured output update.
  /// </summary>
  public IntPtr LastCursorHandle { get; set; }
  /// <summary>
  /// Gets or sets the screen position of the cursor in the last capture, or null when the cursor was hidden.
  /// </summary>
  public Point? LastCursorPosition { get; set; }
  /// <summary>
  /// Gets or sets a value indicating whether the cursor was visible during the last captured output update.
  /// </summary>
  public bool LastCursorVisible { get; set; }
  /// <summary>
  /// Gets the DXGI output duplication interface for frame capture.
  /// </summary>
  public IDXGIOutputDuplication OutputDuplication { get; } = outputDuplication;
  /// <summary>
  /// Gets the rotation applied to this output's content.
  /// </summary>
  public DXGI_MODE_ROTATION Rotation { get; } = rotation;

  /// <summary>
  /// Releases all DirectX resources and the cached SKBitmap.
  /// </summary>
  public void Dispose()
  {
    if (IsDisposed)
    {
      return;
    }

    IsDisposed = true;

    _lastCapturedSkBitmap?.Dispose();

    TryHelper.TryAll(
      OutputDuplication.ReleaseFrame,
      () => Marshal.FinalReleaseComObject(OutputDuplication),
      () => Marshal.FinalReleaseComObject(DeviceContext),
      () => Marshal.FinalReleaseComObject(Device),
      () => Marshal.FinalReleaseComObject(Adapter));

    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Sets the last captured SKBitmap, disposing any previous one.
  /// </summary>
  /// <param name="newSkBitmap">The new SKBitmap to cache, or null to clear.</param>
  public void SetLastCapturedSkBitmap(SKBitmap? newSkBitmap)
  {
    _lastCapturedSkBitmap?.Dispose();
    _lastCapturedSkBitmap = newSkBitmap;
  }
}