using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;

namespace ControlR.Libraries.ScreenCapture.Models;
internal sealed class DxOutput : IDisposable
{
    private bool _isDisposed;

    public DxOutput(
        string deviceName,
        Rectangle bounds,
        IDXGIAdapter1 adapter,
        ID3D11Device device,
        ID3D11DeviceContext deviceContext,
        IDXGIOutputDuplication outputDuplication,
        DXGI_MODE_ROTATION rotation)
    {
        DeviceName = deviceName;
        Bounds = bounds;
        Adapter = adapter;
        Device = device;
        DeviceContext = deviceContext;
        OutputDuplication = outputDuplication;
        Rotation = rotation;
    }

    public IDXGIAdapter1 Adapter { get; }
    public Rectangle Bounds { get; }
    public ID3D11Device Device { get; }
    public ID3D11DeviceContext DeviceContext { get; }
    public string DeviceName { get; }
    public IDXGIOutputDuplication OutputDuplication { get; }
    public DXGI_MODE_ROTATION Rotation { get; }

    public bool IsDisposed => _isDisposed;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            OutputDuplication.ReleaseFrame();
        }
        catch { }
        try
        {
            Marshal.FinalReleaseComObject(OutputDuplication);
            Marshal.FinalReleaseComObject(DeviceContext);
            Marshal.FinalReleaseComObject(Device);
            Marshal.FinalReleaseComObject(Adapter);
        }
        catch { }
        GC.SuppressFinalize(this);
    }
}