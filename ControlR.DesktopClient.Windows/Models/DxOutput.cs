using System.Drawing;
using System.Runtime.InteropServices;
using ControlR.Libraries.Shared.Helpers;
using SkiaSharp;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;

namespace ControlR.DesktopClient.Windows.Models;

/// <summary>
/// Maintains DirectX resources and state for a specific display output, 
/// facilitating high-performance frame duplication.
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
  /// The adapter that provides this output.
  /// </summary>
  public IDXGIAdapter1 Adapter { get; } = adapter;
  /// <summary>
  /// The output's coordinates and size within the virtual desktop.
  /// </summary>
  public Rectangle Bounds { get; } = bounds;
  /// <summary>
  /// The Direct3D 11 device used for frame capture.
  /// </summary>
  public ID3D11Device Device { get; } = device;
  /// <summary>
  /// The device context for issuing GPU commands.
  /// </summary>
  public ID3D11DeviceContext DeviceContext { get; } = deviceContext;
  /// <summary>
  /// The unique system name for this display device.
  /// </summary>
  public string DeviceName { get; } = deviceName;
  /// <summary>
  /// Indicates if the resources for this output have been released.
  /// </summary>
  public bool IsDisposed { get; private set; }
  /// <summary>
  /// A cached, cursor-free bitmap used for optimizing cursor-only updates.
  /// </summary>
  public SKBitmap? LastCapturedSkBitmap => _lastCapturedSkBitmap;
  /// <summary>
  /// The area occupied by the cursor in the previous capture iteration.
  /// </summary>
  public Rectangle LastCursorArea { get; set; }
  /// <summary>
  /// The system handle for the cursor as of the last update.
  /// </summary>
  public IntPtr LastCursorHandle { get; set; }
  /// <summary>
  /// The screen coordinates of the cursor in the last frame, or null if hidden.
  /// </summary>
  public Point? LastCursorPosition { get; set; }
  /// <summary>
  /// Tracks the visibility state of the cursor in the previous update.
  /// </summary>
  public bool LastCursorVisible { get; set; }
  /// <summary>
  /// The DXGI interface providing frame duplication capabilities.
  /// </summary>
  public IDXGIOutputDuplication OutputDuplication { get; } = outputDuplication;
  /// <summary>
  /// The current hardware rotation of the output.
  /// </summary>
  public DXGI_MODE_ROTATION Rotation { get; } = rotation;

  /// <summary>
  /// Disposes of all COM objects and cached bitmaps.
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
  /// Updates the cached frame, ensuring the previous one is correctly disposed.
  /// </summary>
  /// <param name="newSkBitmap">The new SKBitmap to cache, or null to clear.</param>
  public void SetLastCapturedSkBitmap(SKBitmap? newSkBitmap)
  {
    _lastCapturedSkBitmap?.Dispose();
    _lastCapturedSkBitmap = newSkBitmap;
  }
}