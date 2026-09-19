namespace AlgorithmLab.Core.Dataset;

public interface IMasterDatasetProvider
{
    DatasetConfig Config { get; }
    void UpdateConfig(DatasetConfig newConfig);
    double[] GetMasterSlice(int n);
    double[] CloneMasterSlice(int n);
    (double[,] A, double[,] B) GenerateMatrices(int n, int m);
    (string Text, string Pattern) GenerateStringData(int textLength, int patternLength);
    event Action? OnDatasetChanged;
}
