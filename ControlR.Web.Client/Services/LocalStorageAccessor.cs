using Microsoft.JSInterop;

namespace ControlR.Web.Client.Services;

public interface ILocalStorageAccessor
{
  ValueTask Clear();

  ValueTask<string?> GetItem(string key);
  ValueTask RemoveItem(string key);

  ValueTask SetItem(string key, string value);
}

internal class LocalStorageAccessor(IJSRuntime jsRuntime) : ILocalStorageAccessor
{
  private readonly IJSRuntime _jsRuntime = jsRuntime;

  public async ValueTask Clear()
  {
    await _jsRuntime.InvokeVoidAsync("clearLocalStorage");
  }

  public async ValueTask<string?> GetItem(string key)
  {
    return await _jsRuntime.InvokeAsync<string?>("getFromLocalStorage", key);
  }

  public async ValueTask RemoveItem(string key)
  {
    await _jsRuntime.InvokeVoidAsync("removeFromLocalStorage", key);
  }

  public async ValueTask SetItem(string key, string value)
  {
    await _jsRuntime.InvokeVoidAsync("setToLocalStorage", key, value);
  }
}
