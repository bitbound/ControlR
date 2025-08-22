using Microsoft.JSInterop;

namespace ControlR.Web.Client.Services;

public interface ISessionStorageAccessor
{
  ValueTask Clear();

  ValueTask<string?> GetItem(string key);
  ValueTask RemoveItem(string key);

  ValueTask SetItem(string key, string value);
}

internal class SessionStorageAccessor(IJSRuntime jsRuntime) : ISessionStorageAccessor
{
  private readonly IJSRuntime _jsRuntime = jsRuntime;

  public async ValueTask Clear()
  {
    await _jsRuntime.InvokeVoidAsync("clearSessionStorage");
  }

  public async ValueTask<string?> GetItem(string key)
  {
    return await _jsRuntime.InvokeAsync<string?>("getFromSessionStorage", key);
  }

  public async ValueTask RemoveItem(string key)
  {
    await _jsRuntime.InvokeVoidAsync("removeFromSessionStorage", key);
  }

  public async ValueTask SetItem(string key, string value)
  {
    await _jsRuntime.InvokeVoidAsync("setToSessionStorage", key, value);
  }
}
