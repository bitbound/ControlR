using System.Drawing;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dxgi;

namespace ControlR.Streamer.Extensions;
internal static class DxExtensions
{
    public static List<IDXGIAdapter1> GetAdapters(this IDXGIFactory1 factory)
    {
        var adapters = new List<IDXGIAdapter1>();
        try
        {
            while (true)
            {
                var adapterResult = factory.EnumAdapters1((uint)adapters.Count, out var adapter);
                if (!adapterResult.Succeeded)
                {
                    break;
                }

                adapters.Add(adapter);
            }
        }
        catch { }
        return adapters;
    }

    public static List<IDXGIOutput1> GetOutputs(this IDXGIAdapter1 adapter)
    {
        var outputs = new List<IDXGIOutput1>();
        try
        {
            while (true)
            {
                var adapterResult = adapter.EnumOutputs((uint)outputs.Count, out var output);
                if (!adapterResult.Succeeded)
                {
                    break;
                }

                outputs.Add((IDXGIOutput1)output);
            }
        }
        catch { }
        return outputs;
    }

    public static Rectangle ToRectangle(this RECT rect)
    {
        return new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
    }
}
