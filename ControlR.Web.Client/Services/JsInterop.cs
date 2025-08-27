using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ControlR.Web.Client.Services;

public interface IJsInterop
{
  ValueTask AddBeforeUnloadHandler();

  ValueTask AddClassName(ElementReference element, string className);

  ValueTask Alert(string message);

  ValueTask<bool> Confirm(string message);
  ValueTask<string?> CreateBlobUrl(byte[] imageData, string mimeType);
  ValueTask<string> GetClipboardText();

  ValueTask<int> GetCursorIndex(ElementReference inputElement);
  ValueTask<int> GetCursorIndexById(string inputElementId);

  ValueTask InvokeClick(string elementId);
  ValueTask<bool> IsTouchScreen();

  ValueTask Log(string categoryName, LogLevel level, string message);

  ValueTask OpenWindow(string url, string target);

  ValueTask PreventTabOut(ElementReference inputElement);
  ValueTask PreventTabOut(string inputElementId);
  ValueTask<string> Prompt(string message);

  ValueTask Reload();

  ValueTask ScrollToElement(ElementReference element);

  ValueTask ScrollToEnd(ElementReference element);
  ValueTask SetClipboardText(string? text);
  ValueTask SetCursorIndexById(string inputElementId, int cursorPosition);
  ValueTask SetScreenWakeLock(bool isWakeEnabled);

  ValueTask SetStyleProperty(ElementReference element, string propertyName, string value);

  ValueTask StartDraggingY(ElementReference element, double clientY);
  ValueTask ToggleFullscreen(ElementReference element);
}

public class JsInterop(IJSRuntime jsRuntime) : IJsInterop
{
  public ValueTask AddBeforeUnloadHandler()
  {
    return jsRuntime.InvokeVoidAsync("addBeforeUnloadHandler");
  }

  public ValueTask AddClassName(ElementReference element, string className)
  {
    return jsRuntime.InvokeVoidAsync("addClassName", element, className);
  }

  public ValueTask Alert(string message)
  {
    return jsRuntime.InvokeVoidAsync("invokeAlert", message);
  }

  public ValueTask<bool> Confirm(string message)
  {
    return jsRuntime.InvokeAsync<bool>("invokeConfirm", message);
  }

  public ValueTask<string?> CreateBlobUrl(byte[] imageData, string mimeType)
  {
    return jsRuntime.InvokeAsync<string?>("createBlobUrl", imageData, mimeType);
  }

  public ValueTask<string> GetClipboardText()
  {
    return jsRuntime.InvokeAsync<string>("getClipboardText");
  }

  public ValueTask<int> GetCursorIndex(ElementReference inputElement)
  {
    return jsRuntime.InvokeAsync<int>("getSelectionStart", inputElement);
  }

  public ValueTask<int> GetCursorIndexById(string inputElementId)
  {
    return jsRuntime.InvokeAsync<int>("getSelectionStartById", inputElementId);
  }

  public ValueTask InvokeClick(string elementId)
  {
    return jsRuntime.InvokeVoidAsync("invokeClick", elementId);
  }

  public ValueTask<bool> IsTouchScreen()
  {
    return jsRuntime.InvokeAsync<bool>("isTouchScreen");
  }

  public ValueTask Log(string categoryName, LogLevel level, string message)
  {
    return jsRuntime.InvokeVoidAsync("log", categoryName, message);
  }

  public ValueTask OpenWindow(string url, string target)
  {
    return jsRuntime.InvokeVoidAsync("openWindow", url, target);
  }

  public ValueTask PreventTabOut(ElementReference inputElement)
  {
    return jsRuntime.InvokeVoidAsync("preventTabOut", inputElement);
  }

  public ValueTask PreventTabOut(string inputElementId)
  {
    return jsRuntime.InvokeVoidAsync("preventTabOutById", inputElementId);
  }

  public ValueTask<string> Prompt(string message)
  {
    return jsRuntime.InvokeAsync<string>("invokePrompt", message);
  }

  public ValueTask Reload()
  {
    return jsRuntime.InvokeVoidAsync("reload");
  }

  public ValueTask ScrollToElement(ElementReference element)
  {
    return jsRuntime.InvokeVoidAsync("scrollToElement", element);
  }

  public ValueTask ScrollToEnd(ElementReference element)
  {
    return jsRuntime.InvokeVoidAsync("scrollToEnd", element);
  }

  public async ValueTask SetClipboardText(string? text)
  {
    await jsRuntime.InvokeVoidAsync("setClipboardText", text);
  }

  public ValueTask SetCursorIndexById(string inputElementId, int cursorPosition)
  {
    return jsRuntime.InvokeVoidAsync("setSelectionStartById", inputElementId, cursorPosition);
  }

  public ValueTask SetScreenWakeLock(bool isWakeEnabled)
  {
    return jsRuntime.InvokeVoidAsync("setScreenWakeLock", isWakeEnabled);
  }

  public ValueTask SetStyleProperty(ElementReference element, string propertyName, string value)
  {
    return jsRuntime.InvokeVoidAsync("setStyleProperty", element, propertyName, value);
  }

  public ValueTask StartDraggingY(ElementReference element, double clientY)
  {
    return jsRuntime.InvokeVoidAsync("startDraggingY", element, clientY);
  }

  public ValueTask ToggleFullscreen(ElementReference element)
  {
    return jsRuntime.InvokeVoidAsync("toggleFullscreen", element);
  }
}