using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components;

public class JsInteropableComponent : ViewportAwareComponent
{
  private IJSObjectReference? _jsModule;
  private string _jsPath = string.Empty;
  private string _componentName = string.Empty;

  [Inject]
  public required IAppEnvironment AppEnvironment { get; init; }

  [Inject]
  public required IJSRuntime JsRuntime { get; init; }

  protected IJSObjectReference JsModule => _jsModule ?? throw new InvalidOperationException("JS module is not initialized");
  protected ManualResetEventAsync JsModuleReady { get; } = new();

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);

    if (firstRender)
    {
      var componentType = GetType();
      _componentName = componentType.Name;
      var assembly = componentType.Assembly.GetName().Name!;
      _jsPath = componentType.FullName!
        .Replace($"{assembly}", "")
        .Replace(".", "/")
        + $".razor.js?v={GetCacheBuster()}";

      _jsModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", _jsPath);
      
      JsModuleReady.Set();
    }
  }

  [SupportedOSPlatform("browser")]
  protected async Task ImportJsHost()
  {
    await JSHost.ImportAsync(_componentName, _jsPath);
  }

  private string GetCacheBuster()
  {
    // Get the version of the main assembly (e.g., "1.0.0.12345")
    var appVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    // In Development, use a unique Guid to bust cache on every refresh.
    // In Production, use the stable Assembly Version.
    return AppEnvironment.IsDevelopment()
        ? Guid.NewGuid().ToString("N")
        : appVersion;
  }
}
