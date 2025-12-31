using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components;

public class JsInteropableComponent : ViewportAwareComponent
{
  private readonly TaskCompletionSource _jsModuleReady = new();
  private string _componentName = string.Empty;
  private IJSObjectReference? _jsModule;
  private string _jsPath = string.Empty;

  [Inject]
  public required IAppEnvironment AppEnvironment { get; init; }
  [Inject]
  public required IJSRuntime JsRuntime { get; init; }

  protected bool IsJsModuleReady => _jsModuleReady.Task.IsCompleted;
  protected IJSObjectReference JsModule => _jsModule ?? throw new InvalidOperationException("JS module is not initialized");

  [SupportedOSPlatform("browser")]
  protected async Task ImportJsHost()
  {
    await JSHost.ImportAsync(_componentName, _jsPath);
  }
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
      _jsModuleReady.SetResult();
    }
  }

  protected async Task WaitForJsModule(CancellationToken cancellationToken = default)
  {
    await _jsModuleReady.Task.WaitAsync(cancellationToken);
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
