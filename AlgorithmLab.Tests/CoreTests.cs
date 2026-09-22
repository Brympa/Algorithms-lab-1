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

    [Fact]
    public async Task ConstantFunction_And_AdaptiveCalibration_ProducesPositiveMeasurableDuration()
    {
        var provider = new MasterDatasetProvider();
        var engine = new AlgorithmLab.Core.Benchmark.PrecisionBenchmarkEngine(provider);
        var constAlgo = new ConstantFunctionAlgorithm();

        var result = await engine.RunExperimentAsync(
            constAlgo,
            nMin: 1000,
            nMax: 5000,
            step: 2000,
            runsPerN: 3
        );

        Assert.NotEmpty(result.Points);
        foreach (var pt in result.Points)
        {
            Assert.True(pt.AvgMs > 0, $"Point N={pt.N} must have AvgMs > 0, but was {pt.AvgMs}");
        }

        Assert.True(result.TotalDurationMs > 0, "TotalDurationMs must be positive");
        Assert.NotNull(result.CFactor);
        Assert.True(result.CFactor.Value > 0, "C factor must be positive for constant function");
    }

    [Fact]
    public void MatrixMultiplication_RectangularMatrices_CorrectDimensions_And_Multiplication()
    {
        var provider = new MasterDatasetProvider();
        int n = 4, m = 6;
        (var A, var B) = provider.GenerateMatrices(n, m);

        Assert.Equal(n, A.GetLength(0));
        Assert.Equal(m, A.GetLength(1));
        Assert.Equal(m, B.GetLength(0));
        Assert.Equal(n, B.GetLength(1));

        var algo = new MatrixMultiplyAlgorithm();
        var C = algo.Execute(A, B);

        Assert.Equal(n, C.GetLength(0));
        Assert.Equal(n, C.GetLength(1));

        // Проверяем известное значение для единичной матрицы
        double[,] A1 = { { 1, 2, 3 }, { 4, 5, 6 } }; // 2x3
        double[,] B1 = { { 7, 8 }, { 9, 1 }, { 2, 3 } }; // 3x2
        var C1 = algo.Execute(A1, B1);

        // C1[0,0] = 1*7 + 2*9 + 3*2 = 7 + 18 + 6 = 31
        // C1[0,1] = 1*8 + 2*1 + 3*3 = 8 + 2 + 9 = 19
        // C1[1,0] = 4*7 + 5*9 + 6*2 = 28 + 45 + 12 = 85
        // C1[1,1] = 4*8 + 5*1 + 6*3 = 32 + 5 + 18 = 55
        Assert.Equal(31.0, C1[0, 0]);
        Assert.Equal(19.0, C1[0, 1]);
        Assert.Equal(85.0, C1[1, 0]);
        Assert.Equal(55.0, C1[1, 1]);
    }

    [Fact]
    public async Task PointByPointCache_ReusesCachedPoints_And_ComputesMissing()
    {
        var provider = new MasterDatasetProvider();
        var engine = new AlgorithmLab.Core.Benchmark.PrecisionBenchmarkEngine(provider);
        var sumAlgo = new SumFunctionAlgorithm();

        var preCachedPoint = new BenchmarkPoint
        {
            N = 100,
            AvgMs = 0.042,
            MedianMs = 0.042,
            Runs = new() { new() { RunIndex = 1, ElapsedMs = 0.042 } }
        };

        var result = await engine.RunExperimentAsync(
            sumAlgo,
            nMin: 100,
            nMax: 200,
            step: 100,
            runsPerN: 3,
            getCachedPoint: n => Task.FromResult(n == 100 ? preCachedPoint : (BenchmarkPoint?)null)
        );

        Assert.Equal(2, result.Points.Count);
        // Точка 100 взята из кэша ровно со значением 0.042
        Assert.Equal(0.042, result.Points[0].AvgMs);
        // Точка 200 посчитана динамически и > 0
        Assert.True(result.Points[1].AvgMs > 0);
    }

    [Fact]
    public void DbConnectionConfig_BuildsValidConnectionString()
    {
        var cfg = new DbConnectionConfig
        {
            Host = "192.168.1.100",
            Port = 5433,
            Database = "custom_lab",
            Username = "lab_user",
            Password = "secret_password",
            SslMode = "Require"
        };

        string connStr = cfg.BuildConnectionString();
        Assert.Contains("Host=192.168.1.100", connStr);
        Assert.Contains("Port=5433", connStr);
        Assert.Contains("Database=custom_lab", connStr);
        Assert.Contains("Username=lab_user", connStr);
        Assert.Contains("Password=secret_password", connStr);
        Assert.Contains("SSL Mode=Require", connStr);
    }

    [Fact]
    public async Task BenchmarkEngine_Cancellation_And_CooperativeYielding_Works()
    {
        var provider = new MasterDatasetProvider();
        var engine = new AlgorithmLab.Core.Benchmark.PrecisionBenchmarkEngine(provider);
        var poly = new NaivePolynomialAlgorithm();

        using var cts = new CancellationTokenSource();
        int pointsReceived = 0;

        // Отменяем расчет сразу после получения первой точки
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await engine.RunExperimentAsync(
                poly,
                nMin: 20,
                nMax: 500,
                step: 20,
                runsPerN: 3,
                onPointComputed: pt =>
                {
                    pointsReceived++;
                    cts.Cancel(); // Мгновенная отмена
                    return Task.CompletedTask;
                },
                cancellationToken: cts.Token
            );
        });

        Assert.True(pointsReceived >= 1, "At least one point must be computed before cancellation takes effect");
    }
}

