namespace AlgorithmLab.Core.Dataset;

using Math = System.Math;

public class MasterDatasetProvider : IMasterDatasetProvider
{
    private DatasetConfig _config = new();
    private double[] _masterVector = Array.Empty<double>();

    public DatasetConfig Config => _config;
    public event Action? OnDatasetChanged;

    public MasterDatasetProvider()
    {
        InitializeDataset();
    }

    public void UpdateConfig(DatasetConfig newConfig)
    {
        _config = newConfig ?? throw new ArgumentNullException(nameof(newConfig));
        InitializeDataset();
        OnDatasetChanged?.Invoke();
    }

    private void InitializeDataset()
    {
        int size = Math.Max(1000, _config.MaxPrecomputedSize);
        _masterVector = new double[size];
        var rnd = new Random(_config.Seed);

        switch (_config.Distribution)
        {
            case "SortedAscending":
                for (int i = 0; i < size; i++) _masterVector[i] = i * 0.5 + rnd.NextDouble() * 0.1;
                break;
            case "SortedDescending":
                for (int i = 0; i < size; i++) _masterVector[i] = (size - i) * 0.5 + rnd.NextDouble() * 0.1;
                break;
            case "AlmostSorted":
                for (int i = 0; i < size; i++) _masterVector[i] = i * 0.5;
                // Небольшие перестановки
                for (int i = 0; i < size / 20; i++)
                {
                    int idx1 = rnd.Next(size);
                    int idx2 = rnd.Next(size);
                    (_masterVector[idx1], _masterVector[idx2]) = (_masterVector[idx2], _masterVector[idx1]);
                }
                break;
            case "RandomUniform":
            default:
                for (int i = 0; i < size; i++) _masterVector[i] = rnd.NextDouble() * 100.0;
                break;
        }

        // Если в конфиге заданы явные векторы
        if (_config.CustomVectors != null && _config.CustomVectors.Count > 0)
        {
            foreach (var kvp in _config.CustomVectors)
            {
                if (kvp.Key <= size && kvp.Value.Length >= kvp.Key)
                {
                    Array.Copy(kvp.Value, 0, _masterVector, 0, kvp.Key);
                }
            }
        }
    }

    public double[] GetMasterSlice(int n)
    {
        if (n > _masterVector.Length)
        {
            EnsureCapacity(n);
        }
        var slice = new double[n];
        Array.Copy(_masterVector, 0, slice, 0, n);
        return slice;
    }

    public double[] CloneMasterSlice(int n)
    {
        return GetMasterSlice(n);
    }

    public (double[,] A, double[,] B) GenerateMatrices(int n, int m)
    {
        var rnd = new Random(_config.Seed + n * 31 + m * 17);
        var A = new double[n, m];
        var B = new double[m, n];

        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                A[i, j] = rnd.NextDouble() * 10.0;

        for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
                B[i, j] = rnd.NextDouble() * 10.0;

        return (A, B);
    }

    private void EnsureCapacity(int required)
    {
        int newSize = Math.Max(required, _masterVector.Length * 2);
        var newVec = new double[newSize];
        Array.Copy(_masterVector, newVec, _masterVector.Length);
        var rnd = new Random(_config.Seed + _masterVector.Length);
        for (int i = _masterVector.Length; i < newSize; i++)
        {
            newVec[i] = rnd.NextDouble() * 100.0;
        }
        _masterVector = newVec;
    }
}
