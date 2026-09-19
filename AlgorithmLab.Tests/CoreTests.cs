using AlgorithmLab.Core.Algorithms;
using AlgorithmLab.Core.Dataset;
using AlgorithmLab.Core.Math;
using AlgorithmLab.Core.Models;
using Xunit;

namespace AlgorithmLab.Tests;

public class CoreTests
{
    [Fact]
    public void LeastSquares_And_MSE_CalculateCorrectly()
    {
        // Создаем синтетические точки с точно известным C = 2.5 для O(n)
        // g(n) = n, T(n) = 2.5 * n
        var points = new List<BenchmarkPoint>
        {
            new() { N = 10, AvgMs = 25.0 },
            new() { N = 20, AvgMs = 50.0 },
            new() { N = 30, AvgMs = 75.0 },
            new() { N = 40, AvgMs = 100.0 }
        };

        var fit = ApproximationEngine.FitCurve(points, ComplexityType.ON);

        Assert.Equal(2.5, fit.C, 4);
        Assert.True(fit.MSE < 1e-6, $"MSE must be ~0 for exact linear data, but was {fit.MSE}");
        Assert.True(fit.RSquared > 0.999, $"R2 must be ~1.0, but was {fit.RSquared}");
    }

    [Fact]
    public void MasterDataset_EnsuresIdenticalData_AcrossAlgorithms()
    {
        var provider = new MasterDatasetProvider();
        var slice1 = provider.GetMasterSlice(100);
        var slice2 = provider.GetMasterSlice(100);
        var clone = provider.CloneMasterSlice(100);

        Assert.Equal(100, slice1.Length);
        Assert.Equal(100, slice2.Length);
        Assert.Equal(100, clone.Length);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(slice1[i], slice2[i]);
            Assert.Equal(slice1[i], clone[i]);
        }
    }

    [Fact]
    public void StepCounting_MeetsRequirements()
    {
        var iterative = new SimpleIterativePowerAlgorithm();
        var recursive = new RecursivePowerAlgorithm();
        var fastBinary = new FastBinaryPowerAlgorithm();

        int n = 16;
        var rIter = iterative.ExecuteWithSteps(2.0, n);
        var rRec = recursive.ExecuteWithSteps(2.0, n);
        var rFast = fastBinary.ExecuteWithSteps(2.0, n);

        Assert.Equal(65536.0, rIter.Value);
        Assert.Equal(65536.0, rRec.Value);
        Assert.Equal(65536.0, rFast.Value);

        // Итеративный делает ровно n шагов умножения
        Assert.Equal(n, rIter.StepCount);

        // Быстрый бинарный делает O(log n) шагов, что существенно меньше n
        Assert.True(rFast.StepCount < n, $"Fast pow steps {rFast.StepCount} should be < {n}");
    }

    [Fact]
    public void OutlierDetector_CatchesSpikes()
    {
        var runs = new List<PointRun>
        {
            new() { RunIndex = 1, ElapsedMs = 1.0 },
            new() { RunIndex = 2, ElapsedMs = 1.05 },
            new() { RunIndex = 3, ElapsedMs = 0.98 },
            new() { RunIndex = 4, ElapsedMs = 1.02 },
            new() { RunIndex = 5, ElapsedMs = 8.5 } // Искусственный всплеск от GC/ОС
        };

        OutlierDetector.DetectRunOutliers(runs);

        Assert.False(runs[0].IsOutlier);
        Assert.False(runs[1].IsOutlier);
        Assert.False(runs[2].IsOutlier);
        Assert.False(runs[3].IsOutlier);
        Assert.True(runs[4].IsOutlier, "Run 5 must be marked as outlier");
    }

    [Fact]
    public void KmpAlgorithm_MatchesNaiveStringSearch()
    {
        var kmp = new KmpAlgorithm();
        var naive = new NaiveStringSearchAlgorithm();

        string text = "ACGTACGTGACGTACGTACGT";
        string pattern = "TGAC";

        int idxKmp = kmp.Execute(text, pattern);
        int idxNaive = naive.Execute(text, pattern);

        Assert.True(idxKmp >= 0);
        Assert.Equal(idxNaive, idxKmp);
    }
}
