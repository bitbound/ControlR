using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ControlR.Viewer.Components;
public class JsInteropableComponent : ComponentBase
{
    private IJSObjectReference? _jsModule;
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
            var assembly = componentType.Assembly.GetName().Name!;
            var jsPath = componentType.FullName!
                .Replace($"{assembly}", "")
                .Replace(".", "/")
                + ".razor.js";

            _jsModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", jsPath);

            JsModuleReady.Set();
        }
    }
}
