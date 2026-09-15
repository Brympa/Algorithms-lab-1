namespace AlgorithmLab.Visualizer;

public enum BarState
{
    Default,
    Comparing,
    Swapping,
    Pivot,
    Sorted
}

public class StepSnapshot
{
    public int[] Array { get; set; } = System.Array.Empty<int>();
    public int ComparingIndex1 { get; set; } = -1;
    public int ComparingIndex2 { get; set; } = -1;
    public int SwappingIndex1 { get; set; } = -1;
    public int SwappingIndex2 { get; set; } = -1;
    public int PivotIndex { get; set; } = -1;
    public HashSet<int> SortedIndices { get; set; } = new();
    public int Comparisons { get; set; }
    public int Swaps { get; set; }
    public string Message { get; set; } = "";
}

public class BarItem
{
    public int Value { get; set; }
    public BarState State { get; set; }
}
