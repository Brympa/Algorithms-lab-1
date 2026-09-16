using System.Diagnostics;

namespace AlgorithmLab.Benchmark;

public static class Runner
{
    public const int RunsPerN = 5;

    public static List<BenchResult> Run(string key, Action<int>? onProgress = null)
    {
        List<BenchResult> rawResults;
        ComplexityType complexity;

        if (Algorithms.VectorFunctions.TryGetValue(key, out var vecFn))
        {
            rawResults = RunVectorFunction(vecFn.Fn, onProgress);
            complexity = vecFn.Complexity;
        }
        else if (Algorithms.Sorts.TryGetValue(key, out var sortFn))
        {
            rawResults = RunSort(sortFn.Action, key, onProgress);
            complexity = sortFn.Complexity;
        }
        else if (Algorithms.PowAlgorithms.TryGetValue(key, out var powFn))
        {
            rawResults = RunPow(powFn.Fn, onProgress);
            complexity = powFn.Complexity;
        }
        else if (Algorithms.MatrixAlgorithms.TryGetValue(key, out var matFn))
        {
            rawResults = RunMatrix(matFn.Action, onProgress);
            complexity = matFn.Complexity;
        }
        else
        {
            return new List<BenchResult>();
        }

        // Вычисление теоретической аппроксимации T_theo(n) = C * g(n)
        CalculateTheoreticalCurve(rawResults, complexity);

        return rawResults;
    }

    private static List<BenchResult> RunVectorFunction(Func<double[], double> fn, Action<int>? onProgress)
    {
        var results = new List<BenchResult>();
        var rnd = new Random(42);

        // Прогрев JIT
        var dummy = GenerateVector(100, rnd);
        for (int i = 0; i < 20; i++) fn(dummy);

        int nStart = 1, nEnd = 2000, step = 20;

        for (int n = nStart; n <= nEnd; n += step)
        {
            var v = GenerateVector(n, rnd);
            int iterations = Math.Max(50, 100000 / Math.Max(1, n));
            var samples = new double[RunsPerN];

            for (int r = 0; r < RunsPerN; r++)
            {
                var sw = Stopwatch.StartNew();
                for (int it = 0; it < iterations; it++)
                {
                    fn(v);
                }
                sw.Stop();
                samples[r] = sw.Elapsed.TotalMilliseconds / iterations;
            }

            Array.Sort(samples);
            double medianMs = samples[RunsPerN / 2];

            results.Add(new BenchResult { N = n, AvgMs = medianMs });
            onProgress?.Invoke(n);
        }

        return results;
    }

    private static List<BenchResult> RunSort(Action<double[]> sortAction, string algoName, Action<int>? onProgress)
    {
        var results = new List<BenchResult>();
        var rnd = new Random(42);

        // Для Bubble Sort берем n до 600, чтобы браузер не зависал
        int nStart = 10, nEnd = (algoName == "Bubble sort") ? 500 : 2000;
        int step = (algoName == "Bubble sort") ? 20 : 40;

        // Прогрев
        var dummy = GenerateVector(50, rnd);
        for (int i = 0; i < 5; i++) sortAction((double[])dummy.Clone());

        for (int n = nStart; n <= nEnd; n += step)
        {
            var v = GenerateVector(n, rnd);
            int iterations = (n <= 100) ? 50 : (n <= 500 ? 5 : 1);
            var samples = new double[RunsPerN];

            for (int r = 0; r < RunsPerN; r++)
            {
                var sw = Stopwatch.StartNew();
                for (int it = 0; it < iterations; it++)
                {
                    var copy = (double[])v.Clone();
                    sortAction(copy);
                }
                sw.Stop();
                samples[r] = sw.Elapsed.TotalMilliseconds / iterations;
            }

            Array.Sort(samples);
            double medianMs = samples[RunsPerN / 2];

            results.Add(new BenchResult { N = n, AvgMs = medianMs });
            onProgress?.Invoke(n);
        }

        return results;
    }

    private static List<BenchResult> RunPow(Func<double, int, double> powFn, Action<int>? onProgress)
    {
        var results = new List<BenchResult>();
        double baseVal = 1.0001;

        // Прогрев
        for (int i = 0; i < 50; i++) powFn(baseVal, 100);

        int nStart = 1, nEnd = 2000, step = 20;

        for (int n = nStart; n <= nEnd; n += step)
        {
            int iterations = 10000;
            var samples = new double[RunsPerN];

            for (int r = 0; r < RunsPerN; r++)
            {
                var sw = Stopwatch.StartNew();
                for (int it = 0; it < iterations; it++)
                {
                    powFn(baseVal, n);
                }
                sw.Stop();
                samples[r] = sw.Elapsed.TotalMilliseconds / iterations;
            }

            Array.Sort(samples);
            double medianMs = samples[RunsPerN / 2];

            results.Add(new BenchResult { N = n, AvgMs = medianMs });
            onProgress?.Invoke(n);
        }

        return results;
    }

    private static List<BenchResult> RunMatrix(Action<double[,], double[,]> matAction, Action<int>? onProgress)
    {
        var results = new List<BenchResult>();
        var rnd = new Random(42);

        // Для матриц O(n^3) n от 2 до 120
        int nStart = 5, nEnd = 120, step = 5;

        // Прогрев
        var mA = GenerateMatrix(10, rnd);
        var mB = GenerateMatrix(10, rnd);
        for (int i = 0; i < 3; i++) matAction(mA, mB);

        for (int n = nStart; n <= nEnd; n += step)
        {
            var A = GenerateMatrix(n, rnd);
            var B = GenerateMatrix(n, rnd);

            int iterations = (n <= 30) ? 20 : (n <= 70 ? 3 : 1);
            var samples = new double[RunsPerN];

            for (int r = 0; r < RunsPerN; r++)
            {
                var sw = Stopwatch.StartNew();
                for (int it = 0; it < iterations; it++)
                {
                    matAction(A, B);
                }
                sw.Stop();
                samples[r] = sw.Elapsed.TotalMilliseconds / iterations;
            }

            Array.Sort(samples);
            double medianMs = samples[RunsPerN / 2];

            results.Add(new BenchResult { N = n, AvgMs = medianMs });
            onProgress?.Invoke(n);
        }

        return results;
    }

    private static void CalculateTheoreticalCurve(List<BenchResult> results, ComplexityType complexity)
    {
        if (results.Count == 0) return;

        double EvaluateG(int n) => complexity switch
        {
            ComplexityType.O1 => 1.0,
            ComplexityType.OLogN => Math.Log2(n + 1),
            ComplexityType.ON => n,
            ComplexityType.ONLogN => n * Math.Log2(n + 1),
            ComplexityType.ON2 => (double)n * n,
            ComplexityType.ON3 => (double)n * n * n,
            _ => n
        };

        double sumEmp = 0;
        double sumG = 0;

        foreach (var r in results)
        {
            double g = EvaluateG(r.N);
            sumEmp += r.AvgMs;
            sumG += g;
        }

        double c = (sumG > 0) ? (sumEmp / sumG) : 0;

        foreach (var r in results)
        {
            r.TheoMs = c * EvaluateG(r.N);
        }
    }

    private static double[] GenerateVector(int n, Random rnd)
    {
        var v = new double[n];
        for (int i = 0; i < n; i++) v[i] = rnd.NextDouble() * 100;
        return v;
    }

    private static double[,] GenerateMatrix(int n, Random rnd)
    {
        var M = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                M[i, j] = rnd.NextDouble() * 10;
        return M;
    }
}