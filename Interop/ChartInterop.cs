using Microsoft.JSInterop;

namespace AlgorithmLab.Interop;

public class ChartInterop
{
    private readonly IJSRuntime _js;

    public ChartInterop(IJSRuntime js) => _js = js;

    public async Task InitLiveChartAsync(string canvasId, string title = "", string yAxisLabel = "Время (мс)", bool isStepCounting = false)
    {
        try
        {
            await _js.InvokeVoidAsync("initLiveChart", canvasId, title, yAxisLabel, isStepCounting);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChartInterop] InitLiveChartAsync: {ex.Message}");
        }
    }

    public async Task AppendLivePointAsync(string canvasId, int n, double val, bool isOutlier = false)
    {
        try
        {
            await _js.InvokeVoidAsync("appendLivePoint", canvasId, n, val, isOutlier);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChartInterop] AppendLivePointAsync: {ex.Message}");
        }
    }

    public async Task FinalizeChartAsync(string canvasId, List<int> labels, List<double> empData, List<double>? theoData = null, List<int>? outlierIndices = null)
    {
        try
        {
            await _js.InvokeVoidAsync("finalizeChart", canvasId, labels, empData, theoData, outlierIndices);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChartInterop] FinalizeChartAsync: {ex.Message}");
        }
    }

    public async Task RenderMultiSeriesAsync(string canvasId, List<int> labels, List<SeriesData> seriesList, string yLabel = "Время (мс)")
    {
        try
        {
            await _js.InvokeVoidAsync("renderMultiSeriesChart", canvasId, labels, seriesList, yLabel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChartInterop] RenderMultiSeriesAsync: {ex.Message}");
        }
    }
}

public class SeriesData
{
    public string Name { get; set; } = string.Empty;
    public List<double> Data { get; set; } = new();
}