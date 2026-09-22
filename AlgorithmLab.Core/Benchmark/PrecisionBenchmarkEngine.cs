using System.Diagnostics;
using AlgorithmLab.Core.Algorithms;
using AlgorithmLab.Core.Dataset;
using AlgorithmLab.Core.Math;
using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Benchmark;

public class PrecisionBenchmarkEngine
{
    private readonly IMasterDatasetProvider _datasetProvider;

    public PrecisionBenchmarkEngine(IMasterDatasetProvider datasetProvider)
    {
        _datasetProvider = datasetProvider ?? throw new ArgumentNullException(nameof(datasetProvider));
    }

    /// <summary>
    /// Прогрев JIT / WebAssembly перед замерами для устранения паразитных задержек компиляции
    /// </summary>
    public async Task WarmupAsync(IAlgorithm algorithm)
    {
        await Task.Yield();
        try
        {
            if (algorithm is IVectorAlgorithm vec)
            {
                var dummy = _datasetProvider.GetMasterSlice(50);
                for (int i = 0; i < 5; i++) vec.Execute(dummy);
            }
            else if (algorithm is ISortAlgorithm sort)
            {
                for (int i = 0; i < 3; i++)
                {
                    var dummy = _datasetProvider.CloneMasterSlice(30);
                    sort.Execute(dummy);
                }
            }
            else if (algorithm is IMatrixAlgorithm mat)
            {
                (var A, var B) = _datasetProvider.GenerateMatrices(10, 10);
                mat.Execute(A, B);
            }
            else if (algorithm is IStringSearchAlgorithm str)
            {
                (var text, var pat) = _datasetProvider.GenerateStringData(100, 10);
                str.Execute(text, pat);
            }
            else if (algorithm is IStepCountableAlgorithm step)
            {
                step.ExecuteWithSteps(1.5, 20);
            }
        }
        catch
        {
            // Прогрев не должен приводить к падению
        }
    }

    /// <summary>
    /// Асинхронный изолированный запуск с потоковой передачей точек в UI, точечным кэшем и замером полного времени
    /// </summary>
    public async Task<ExperimentRecord> RunExperimentAsync(
        IAlgorithm algorithm,
        int nMin,
        int nMax,
        int step,
        int runsPerN,
        Func<BenchmarkPoint, Task>? onPointComputed = null,
        Func<int, Task<BenchmarkPoint?>>? getCachedPoint = null,
        CancellationToken cancellationToken = default)
    {
        if (algorithm == null) throw new ArgumentNullException(nameof(algorithm));
        if (step <= 0) step = 10;
        if (runsPerN <= 0) runsPerN = 5;
        if (nMin <= 0) nMin = 1;
        if (nMax < nMin) nMax = nMin + step;

        var totalStopwatch = Stopwatch.StartNew();

        await WarmupAsync(algorithm);

        var experiment = new ExperimentRecord
        {
            AlgorithmId = algorithm.Id,
            AlgorithmName = algorithm.Name,
            Category = algorithm.Category,
            Complexity = algorithm.Complexity,
            ComplexityDisplay = algorithm.ComplexityDisplay,
            NMin = nMin,
            NMax = nMax,
            Step = step,
            RunsPerN = runsPerN,
            ConfigHash = _datasetProvider.Config.ComputeHash()
        };

        var allPoints = new List<BenchmarkPoint>();

        for (int n = nMin; n <= nMax; n += step)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BenchmarkPoint? pt = null;

            // 1. Проверка точечного кэша (если передан поставщик кэша)
            if (getCachedPoint != null)
            {
                pt = await getCachedPoint(n);
            }

            // 2. Если точки нет в кэше — производим замер
            if (pt == null)
            {
                if (algorithm is IStepCountableAlgorithm stepAlgo)
                {
                    // Замер шагов по Части IV ТЗ
                    var stepRes = stepAlgo.ExecuteWithSteps(_datasetProvider.Config.PowerBase, n);
                    pt = new BenchmarkPoint
                    {
                        N = n,
                        StepCount = stepRes.StepCount,
                        AvgMs = 0,
                        MedianMs = 0
                    };
                }
                else
                {
                    // Эмпирический замер времени с изоляцией аллокаций
                    pt = await MeasureTimeForNAsync(algorithm, n, runsPerN, cancellationToken);
                }
            }

            allPoints.Add(pt);

            // Немедленная потоковая передача точки в UI для Live-отрисовки
            if (onPointComputed != null)
            {
                await onPointComputed(pt);
            }

            // КРИТИЧНО ДЛЯ ПРЕДОТВРАЩЕНИЯ ЗАВИСАНИЯ ВКЛАДКИ:
            // Отдаем управление браузеру на 1 кадр (1 мс), чтобы JS-движок перерисовал DOM и обработал клики
            await Task.Delay(1, cancellationToken);
        }

        // Постобработка: поиск выбросов по всей серии
        OutlierDetector.DetectPointOutliers(allPoints);

        // Расчет аппроксимации методом наименьших квадратов и MSE
        if (algorithm is not IStepCountableAlgorithm)
        {
            var approx = ApproximationEngine.FitCurve(allPoints, algorithm.Complexity);
            experiment.CFactor = approx.C;
            experiment.MSE = approx.MSE;
            experiment.RMSE = approx.RMSE;
            experiment.RSquared = approx.RSquared;
        }

        totalStopwatch.Stop();
        experiment.TotalDurationMs = totalStopwatch.Elapsed.TotalMilliseconds;
        experiment.Points = allPoints;
        return experiment;
    }

    /// <summary>
    /// Замер матрицы в 3D (n и m) для Части II ТЗ
    /// </summary>
    public async Task<List<BenchmarkPoint>> RunMatrix3DAsync(
        IMatrixAlgorithm matAlgo,
        int nMin, int nMax, int stepN,
        int mMin, int mMax, int stepM,
        Func<BenchmarkPoint, Task>? onPointComputed = null,
        CancellationToken cancellationToken = default)
    {
        var points = new List<BenchmarkPoint>();

        for (int n = nMin; n <= nMax; n += stepN)
        {
            for (int m = mMin; m <= mMax; m += stepM)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 1. Подготовка данных ДО таймера
                (var A, var B) = _datasetProvider.GenerateMatrices(n, m);

                // 2. Стабилизация памяти
                GC.Collect(0, GCCollectionMode.Optimized);

                // 3. Замер
                long start = Stopwatch.GetTimestamp();
                matAlgo.Execute(A, B);
                long end = Stopwatch.GetTimestamp();

                double elapsedMs = (double)(end - start) * 1000.0 / Stopwatch.Frequency;

                var pt = new BenchmarkPoint
                {
                    N = n,
                    M = m,
                    AvgMs = elapsedMs,
                    MedianMs = elapsedMs
                };

                points.Add(pt);

                if (onPointComputed != null)
                {
                    await onPointComputed(pt);
                }

                await Task.Delay(1, cancellationToken);
            }
        }

        return points;
    }

    private async Task<BenchmarkPoint> MeasureTimeForNAsync(IAlgorithm algorithm, int n, int runsPerN, CancellationToken cancellationToken = default)
    {
        int iterations = CalculateIterations(algorithm.Complexity, n);
        var runs = new List<PointRun>(runsPerN);

        // 1. ИЗОЛЯЦИЯ: Предварительная аллокация и клонирование данных ДО замера
        if (algorithm is ISortAlgorithm sortAlgo)
        {
            for (int r = 0; r < runsPerN; r++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var copies = new double[iterations][];
                for (int it = 0; it < iterations; it++)
                {
                    copies[it] = _datasetProvider.CloneMasterSlice(n);
                }

                GC.Collect(0, GCCollectionMode.Optimized);

                long start = Stopwatch.GetTimestamp();
                for (int it = 0; it < iterations; it++)
                {
                    sortAlgo.Execute(copies[it]);
                }
                long end = Stopwatch.GetTimestamp();

                double totalMs = (double)(end - start) * 1000.0 / Stopwatch.Frequency;
                double perIterationMs = totalMs / iterations;

                runs.Add(new PointRun { RunIndex = r + 1, ElapsedMs = perIterationMs });
                await Task.Yield();
            }
        }
        else if (algorithm is IVectorAlgorithm vecAlgo)
        {
            var vector = _datasetProvider.GetMasterSlice(n);

            for (int r = 0; r < runsPerN; r++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GC.Collect(0, GCCollectionMode.Optimized);

                double sink = 0.0;
                long start = Stopwatch.GetTimestamp();
                for (int it = 0; it < iterations; it++)
                {
                    sink += vecAlgo.Execute(vector);
                }
                long end = Stopwatch.GetTimestamp();
                if (sink == 123456789.987) GC.KeepAlive(sink);

                double totalMs = (double)(end - start) * 1000.0 / Stopwatch.Frequency;
                double perIterationMs = totalMs / iterations;

                runs.Add(new PointRun { RunIndex = r + 1, ElapsedMs = perIterationMs });
                await Task.Yield();
            }
        }
        else if (algorithm is IMatrixAlgorithm matAlgo)
        {
            (var A, var B) = _datasetProvider.GenerateMatrices(n, n);

            for (int r = 0; r < runsPerN; r++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GC.Collect(0, GCCollectionMode.Optimized);

                long start = Stopwatch.GetTimestamp();
                for (int it = 0; it < iterations; it++)
                {
                    matAlgo.Execute(A, B);
                }
                long end = Stopwatch.GetTimestamp();

                double totalMs = (double)(end - start) * 1000.0 / Stopwatch.Frequency;
                double perIterationMs = totalMs / iterations;

                runs.Add(new PointRun { RunIndex = r + 1, ElapsedMs = perIterationMs });
                await Task.Yield();
            }
        }
        else if (algorithm is IStringSearchAlgorithm strAlgo)
        {
            (var text, var pat) = _datasetProvider.GenerateStringData(n, System.Math.Max(5, n / 10));

            for (int r = 0; r < runsPerN; r++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GC.Collect(0, GCCollectionMode.Optimized);

                long start = Stopwatch.GetTimestamp();
                for (int it = 0; it < iterations; it++)
                {
                    strAlgo.Execute(text, pat);
                }
                long end = Stopwatch.GetTimestamp();

                double totalMs = (double)(end - start) * 1000.0 / Stopwatch.Frequency;
                double perIterationMs = totalMs / iterations;

                runs.Add(new PointRun { RunIndex = r + 1, ElapsedMs = perIterationMs });
                await Task.Yield();
            }
        }

        // Детекция выбросов среди прогонов
        OutlierDetector.DetectRunOutliers(runs);

        double avgMs = runs.Average(r => r.ElapsedMs);
        var sortedRuns = runs.OrderBy(r => r.ElapsedMs).ToList();
        double medianMs = sortedRuns[runs.Count / 2].ElapsedMs;

        return new BenchmarkPoint
        {
            N = n,
            AvgMs = avgMs,
            MedianMs = medianMs,
            Runs = runs,
            IsOutlier = runs.Any(r => r.IsOutlier && r.ElapsedMs > 2.0 * medianMs)
        };
    }

    private static int CalculateIterations(ComplexityType complexity, int n)
    {
        return complexity switch
        {
            ComplexityType.O1 => 100_000, // 100 000 итераций обеспечивают статистически значимое время (5-15 мс), полностью устраняя квантование браузерного таймера
            ComplexityType.OLogN => 20_000,
            ComplexityType.ON => System.Math.Max(10, 50_000 / System.Math.Max(1, n)),
            ComplexityType.ONLogN => System.Math.Max(5, 10_000 / System.Math.Max(1, n)),
            ComplexityType.ON2 => (n <= 100) ? 10 : (n <= 300 ? 3 : 1),
            ComplexityType.ON3 => (n <= 30) ? 5 : 1,
            _ => 1
        };
    }
}
