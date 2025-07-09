using ControlR.Streamer.Extensions;
using ControlR.Streamer.Helpers;
using ControlR.Streamer.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Dxgi;

namespace ControlR.Streamer.Services;

internal interface IDxOutputGenerator
{
  DxOutput? GetDxOutput(string deviceName);
  void RefreshOutput();
}

internal class DxOutputGenerator : IDxOutputGenerator
{
  private readonly ILogger<DxOutputGenerator> _logger;
  private DxOutput? _currentOutput;
  private readonly MemoryCache _faultedDevices = new(new MemoryCacheOptions());

  public DxOutputGenerator(ILogger<DxOutputGenerator> logger)
  {
    _logger = logger;
    AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
  }

  public DxOutput? GetDxOutput(string deviceName)
  {
    try
    {
      // Fall back to BitBlt for faulted DX devices. After the
      // cached item expires, we will try to create a new DX device.
      // This prevents going into a continual loop of trying to
      // create DX devices in situations like when the laptop lid
      // is closed.
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

      var factory = (IDXGIFactory1)factoryObj;
      var adapters = factory.GetAdapters();

      foreach (var adapter in adapters)
      {
        foreach (var output in adapter.GetOutputs())
        {
          unsafe
          {
            var outputDescription = output.GetDesc();
            var outputDeviceName = outputDescription.DeviceName.ToString();

            if (outputDescription.DeviceName.ToString() != deviceName)
            {
              continue;
            }

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
                  FeatureLevels: 7,
                  SDKVersion: 7,
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
              break;
            }
          }
        }
      }

      Marshal.FinalReleaseComObject(factoryObj);
      foreach (var adapter in adapters)
      {
        Marshal.FinalReleaseComObject(adapter);
      }

      return _currentOutput;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while getting DxOutput for device name {DeviceName}.", deviceName);
      return null;
    }
  }

  public void RefreshOutput()
  {
    if (_currentOutput is null)
    {
      return;
    }

    _faultedDevices.Set(
      _currentOutput.DeviceName,
      _currentOutput.DeviceName,
      TimeSpan.FromSeconds(10));

    Disposer.TryDispose(_currentOutput);
    _currentOutput = null;
  }

  private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
  {
    Disposer.TryDispose([_currentOutput]);
  }
}
