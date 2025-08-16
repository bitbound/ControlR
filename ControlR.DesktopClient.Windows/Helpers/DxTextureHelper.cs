using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi.Common;

internal static class DxTextureHelper
{
  public static D3D11_TEXTURE2D_DESC Create2dTextureDescription(int width, int height)
  {
    return new D3D11_TEXTURE2D_DESC()
    {
      CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ,
      BindFlags = 0,
      Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
      Width = (uint)width,
      Height = (uint)height,
      MiscFlags = 0,
      MipLevels = 1,
      ArraySize = 1,
      SampleDesc = { Count = 1, Quality = 0 },
      Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
    };
  }
}
