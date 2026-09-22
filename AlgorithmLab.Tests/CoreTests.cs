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
    public void PancakeSort_SortsCorrectly()
    {
        var algo = new PancakeSortAlgorithm();
        double[] array = { 67.2, 12.5, 89.1, 0.4, 45.3, -5.2, 12.5, 100.0 };
        double[] expected = (double[])array.Clone();
        Array.Sort(expected);

        algo.Execute(array);

        Assert.Equal(expected, array);
    }

    [Fact]
    public void CocktailShakerSort_SortsCorrectly()
    {
        var algo = new CocktailShakerSortAlgorithm();
        double[] array = { 45.0, 10.0, 78.5, 3.2, 99.9, -12.0, 10.0, 50.1 };
        double[] expected = (double[])array.Clone();
        Array.Sort(expected);

        algo.Execute(array);

        Assert.Equal(expected, array);
    }

    [Fact]
    public void CombSort_SortsCorrectly()
    {
        var algo = new CombSortAlgorithm();
        double[] array = { 88.0, 12.0, 4.0, 99.0, 23.0, 1.0, -10.0, 4.0, 105.0 };
        double[] expected = (double[])array.Clone();
        Array.Sort(expected);

        algo.Execute(array);

        Assert.Equal(expected, array);
    }

    [Fact]
    public void ApproximationEngine_CalculatesCV_ForO1()
    {
        // 10 точек с небольшим разбросом вокруг среднего 0.0050 мс
        var points = new List<BenchmarkPoint>
        {
            new() { N = 100, AvgMs = 0.0050 },
            new() { N = 200, AvgMs = 0.0051 },
            new() { N = 300, AvgMs = 0.0049 },
            new() { N = 400, AvgMs = 0.0050 },
            new() { N = 500, AvgMs = 0.0052 }
        };

        var fit = ApproximationEngine.FitCurve(points, ComplexityType.O1);

        Assert.NotNull(fit.CV);
        // Вариация должна быть строго положительной и менее 10%
        Assert.InRange(fit.CV.Value, 0.01, 10.0);
    }

    [Fact]
    public async Task BenchmarkEngine_Guarantees_NMax_Inclusion()
    {
        var provider = new MasterDatasetProvider();
        var engine = new AlgorithmLab.Core.Benchmark.PrecisionBenchmarkEngine(provider);
        var algo = new SumFunctionAlgorithm();

        // 100 .. 1000 с шагом 300: 100, 400, 700, 1000 (1000 кратен)
        // 100 .. 10000 с шагом 500: 100, 600, ..., 9600 -> должен включить 10000!
        var result = await engine.RunExperimentAsync(
            algo,
            nMin: 100,
            nMax: 10000,
            step: 500,
            runsPerN: 1
        );

        Assert.Equal(10000, result.Points.Last().N);
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

    [Fact]
    public void OutlierDetector_DetectsConstantFunctionSpikes_And_ApproximationExcludesOutliers()
    {
        // Базовый уровень 0.000020 мс для O(1)
        var points = new List<BenchmarkPoint>();
        for (int i = 1; i <= 30; i++)
        {
            points.Add(new BenchmarkPoint
            {
                N = i * 10,
                AvgMs = 0.000020 + (i % 3) * 0.000001,
                MedianMs = 0.000020
            });
        }

        // Добавляем 4 явных выброса (всплески ОС / сборки мусора до 0.000080 - 0.000120 мс)
        points[5].AvgMs = 0.000095;
        points[5].MedianMs = 0.000095;
        points[12].AvgMs = 0.000120;
        points[12].MedianMs = 0.000120;
        points[20].AvgMs = 0.000080;
        points[20].MedianMs = 0.000080;
        points[27].AvgMs = 0.000110;
        points[27].MedianMs = 0.000110;

        OutlierDetector.DetectPointOutliers(points, ComplexityType.O1);

        Assert.True(points[5].IsOutlier, "Point 5 spike (0.000095 ms) must be flagged as outlier");
        Assert.True(points[12].IsOutlier, "Point 12 spike (0.000120 ms) must be flagged as outlier");
        Assert.True(points[20].IsOutlier, "Point 20 spike (0.000080 ms) must be flagged as outlier");
        Assert.True(points[27].IsOutlier, "Point 27 spike (0.000110 ms) must be flagged as outlier");

        // Не-выбросы не должны быть ложно помечены
        Assert.False(points[0].IsOutlier, "Normal baseline point must not be outlier");
        Assert.False(points[10].IsOutlier, "Normal baseline point must not be outlier");

        // Аппроксимация должна игнорировать выбросы при расчете C
        var fit = ApproximationEngine.FitCurve(points, ComplexityType.O1);
        Assert.InRange(fit.C, 0.000019, 0.000023);
    }

    [Fact]
    public void IsOnlinePointOutlier_FlagsIncomingSpikes_AgainstRunningBaseline()
    {
        var runningPoints = new List<BenchmarkPoint>();
        for (int i = 1; i <= 10; i++)
        {
            runningPoints.Add(new BenchmarkPoint
            {
                N = i * 10,
                AvgMs = 0.000020,
                MedianMs = 0.000020,
                IsOutlier = false
            });
        }

        var spikePoint = new BenchmarkPoint
        {
            N = 110,
            AvgMs = 0.000095,
            MedianMs = 0.000095
        };

        bool isSpike = OutlierDetector.IsOnlinePointOutlier(spikePoint, runningPoints, ComplexityType.O1);
        Assert.True(isSpike, "Incoming spike of 0.000095 ms over 0.000020 ms baseline must be detected online");

        var normalPoint = new BenchmarkPoint
        {
            N = 120,
            AvgMs = 0.000021,
            MedianMs = 0.000020
        };
        bool isNormal = OutlierDetector.IsOnlinePointOutlier(normalPoint, runningPoints, ComplexityType.O1);
        Assert.False(isNormal, "Normal point close to baseline must not be detected as outlier");
    }
}

