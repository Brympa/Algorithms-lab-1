using Microsoft.JSInterop;

namespace AlgorithmLab.Interop;

public class ChartInterop
{
    private readonly IJSRuntime _js;
    public ChartInterop(IJSRuntime js) => _js = js;

    public async Task RenderAsync(string canvasId, List<int> labels, List<double> empData, List<double> theoData)
    {
        await _js.InvokeVoidAsync("renderChart", canvasId, labels, empData, theoData);
    }
}