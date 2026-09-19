using Microsoft.JSInterop;

namespace AlgorithmLab.Interop;

public class Plotly3DInterop
{
    private readonly IJSRuntime _js;

    public Plotly3DInterop(IJSRuntime js) => _js = js;

    public async Task RenderSurfaceAsync(string elementId, List<int> nValues, List<int> mValues, double[][] zMatrix)
    {
        try
        {
            await _js.InvokeVoidAsync("renderPlotly3DSurface", elementId, nValues, mValues, zMatrix);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Plotly3DInterop] RenderSurfaceAsync: {ex.Message}");
        }
    }
}
