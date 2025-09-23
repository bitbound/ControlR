using System.Runtime.InteropServices;
using ControlR.DesktopClient.Windows.Extensions;
using ControlR.DesktopClient.Windows.Models;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Dxgi;

namespace ControlR.DesktopClient.Windows.Services;

internal interface IDxOutputDuplicator : IDisposable
{
  DxOutput? DuplicateOutput(string deviceName);
  void SetCurrentOutputFaulted();
}

internal class DxOutputDuplicator(ILogger<DxOutputDuplicator> logger) : IDxOutputDuplicator
{
  private readonly MemoryCache _faultedDevices = new(new MemoryCacheOptions());
  private readonly ILogger<DxOutputDuplicator> _logger = logger;
  private DxOutput? _currentOutput;
  private bool _disposedValue;

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

  public DxOutput? DuplicateOutput(string deviceName)
  {
    try
    {
      // If the device has faulted recently, skip trying to recreate it.
      // This prevents repeated attempts to access a faulty device.
      // This can happen when a laptop lid is closed or when switching sessions.
      if (_faultedDevices.TryGetValue(deviceName, out var _))
      {
        return null;
      }

      if (_currentOutput?.DeviceName == deviceName)
      {
        return _currentOutput;
      }

      _currentOutput?.Dispose();
      _currentOutput = null;

      var factoryGuid = typeof(IDXGIFactory1).GUID;
      var factoryResult = PInvoke.CreateDXGIFactory1(factoryGuid, out var factoryObj);
      if (factoryResult.Failed)
      {
        _logger.LogWarning("Failed to create DXGI Factory. Result: {Result}.", factoryResult);
        _faultedDevices.Set(
          deviceName,
          deviceName,
          TimeSpan.FromSeconds(10));
        return null;
      }

      var factory = (IDXGIFactory1)factoryObj;
      using var factoryDisposer = new CallbackDisposable(() =>
      {
        Marshal.FinalReleaseComObject(factory);
      });

      var adapterOutput = FindOutput(factory, deviceName);

      if (adapterOutput is null)
      {
        _logger.LogWarning("Failed to find DXGI Output for device name {DeviceName}.", deviceName);
        _faultedDevices.Set(
          deviceName,
          deviceName,
          TimeSpan.FromSeconds(10));
        return null;
      }

      unsafe
      {
        var (adapter, output) = adapterOutput;
        var outputDescription = output.GetDesc();

        var bounds = outputDescription.DesktopCoordinates.ToRectangle();

        var featureLevelArray = new[]
        {
          D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1,
          D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
          D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1,
          D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0,
          D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_3,
          D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_2,
          D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_1
        };

        fixed (D3D_FEATURE_LEVEL* featureLevelArrayRef = featureLevelArray)
        {
          PInvoke.D3D11CreateDevice(
              pAdapter: adapter,
              DriverType: 0,
              Software: HMODULE.Null,
              Flags: 0,
              pFeatureLevels: featureLevelArrayRef,
              FeatureLevels: (uint)featureLevelArray.Length,
              SDKVersion: PInvoke.D3D11_SDK_VERSION,
              ppDevice: out var device,
              pFeatureLevel: null,
              ppImmediateContext: out var deviceContext);

          output.DuplicateOutput(device, out var outputDuplication);

          _currentOutput = new DxOutput(
              deviceName,
              bounds,
              adapter,
              device,
              deviceContext,
              outputDuplication,
              outputDescription.Rotation);
        }
      }

      return _currentOutput;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting DxOutput for device name {DeviceName}.", deviceName);
      _faultedDevices.Set(
        deviceName,
        deviceName,
        TimeSpan.FromSeconds(10));
      return null;
    }
  }

  public void SetCurrentOutputFaulted()
  {
    if (_currentOutput is null)
    {
      return;
    }

    _faultedDevices.Set(
      _currentOutput.DeviceName,
      _currentOutput.DeviceName,
      TimeSpan.FromSeconds(10));

    Disposer.DisposeAll(_currentOutput);
    _currentOutput = null;
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposedValue)
    {
      if (disposing)
      {
        Disposer.DisposeAll([_currentOutput]);
      }
      _disposedValue = true;
    }
  }

  private static DxAdapterOutput? FindOutput(IDXGIFactory1 factory, string deviceName)
  {
    for (uint adapterIdex = 0; factory.EnumAdapters1(adapterIdex, out var adapter).Succeeded; adapterIdex++)
    {
      for (uint outputIndex = 0; adapter.EnumOutputs(outputIndex, out var output).Succeeded; outputIndex++)
      {
        var outputDescription = output.GetDesc();
        var outputDeviceName = outputDescription.DeviceName.ToString();

        if (outputDeviceName == deviceName)
        {
          return new DxAdapterOutput(adapter, (IDXGIOutput1)output);
        }

        Marshal.FinalReleaseComObject(output);
      }

      Marshal.FinalReleaseComObject(adapter);
    }
    return null;
  }

  private sealed record DxAdapterOutput(IDXGIAdapter1 Adapter, IDXGIOutput1 Output) : IDisposable
  {
    public void Dispose()
    {
      Marshal.FinalReleaseComObject(Output);
      Marshal.FinalReleaseComObject(Adapter);
    }
  }

}
