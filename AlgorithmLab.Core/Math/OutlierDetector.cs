using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Math;

public static class OutlierDetector
{
    /// <summary>
    /// Детекция выбросов внутри набора независимых прогонов для одной точки N
    /// </summary>
    public static void DetectRunOutliers(List<PointRun> runs)
    {
        if (runs == null || runs.Count < 3) return;

        var sorted = runs.OrderBy(r => r.ElapsedMs).ToList();
        double median = sorted[sorted.Count / 2].ElapsedMs;
        double q1 = GetPercentile(sorted.Select(r => r.ElapsedMs).ToList(), 0.25);
        double q3 = GetPercentile(sorted.Select(r => r.ElapsedMs).ToList(), 0.75);
        double iqr = q3 - q1;

        double mean = runs.Average(r => r.ElapsedMs);

        foreach (var run in runs)
        {
            // Всплеск относительно медианы или IQR
            bool isIqrOutlier = (iqr > 1e-7) && (run.ElapsedMs > q3 + 1.5 * iqr);
            bool isRelativeOutlier = (median > 1e-7) && (run.ElapsedMs > 1.35 * median);
            bool isSpikeOutlier = (mean > 1e-7) && (run.ElapsedMs > 1.4 * mean);

            run.IsOutlier = isIqrOutlier || isRelativeOutlier || isSpikeOutlier;
        }
    }

    /// <summary>
    /// Проверка точки в реальном времени (Online Detection) относительно накопленного базового уровня
    /// </summary>
    public static bool IsOnlinePointOutlier(BenchmarkPoint currentPoint, IReadOnlyList<BenchmarkPoint> existingPoints, ComplexityType complexity)
    {
        if (currentPoint == null) return false;

        // 1. Проверка внутри прогонов точки
        if (currentPoint.Runs.Any(r => r.IsOutlier) && currentPoint.MedianMs > 1e-7 && currentPoint.AvgMs > 1.25 * currentPoint.MedianMs)
        {
            return true;
        }

        // 2. Проверка относительно скользящего базового уровня существующих точек
        if (existingPoints != null && existingPoints.Count >= 3)
        {
            var recent = existingPoints.TakeLast(30).ToList();
            var cleanRecent = recent.Where(p => !p.IsOutlier).ToList();
            var baselinePool = cleanRecent.Count >= 3 ? cleanRecent : recent;

            if (complexity == ComplexityType.O1)
            {
                var sorted = baselinePool.Select(p => p.AvgMs).OrderBy(x => x).ToList();
                double baselineMedian = sorted[sorted.Count / 2];

                // MAD (Median Absolute Deviation)
                var deviations = sorted.Select(x => System.Math.Abs(x - baselineMedian)).OrderBy(d => d).ToList();
                double mad = deviations[deviations.Count / 2];

                double threshold = System.Math.Max(baselineMedian * 1.20, baselineMedian + 2.5 * mad);
                if (baselineMedian > 1e-7 && (currentPoint.AvgMs > threshold || currentPoint.MedianMs > threshold))
                {
                    return true;
                }
            }
            else
            {
                // Для растущих функций сравниваем нормализованное отношение T_i / g(n_i)
                double currentG = ApproximationEngine.EvaluateG(complexity, currentPoint.N);
                double currentRatio = currentPoint.AvgMs / currentG;

                var ratios = baselinePool.Select(p => p.AvgMs / ApproximationEngine.EvaluateG(complexity, p.N)).OrderBy(x => x).ToList();
                double medianRatio = ratios[ratios.Count / 2];

                if (medianRatio > 1e-15 && currentRatio > 1.25 * medianRatio)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Полномасштабная робастная детекция выбросов по всей серии точек с использованием MAD
    /// </summary>
    public static void DetectPointOutliers(List<BenchmarkPoint> points, ComplexityType complexity = ComplexityType.O1)
    {
        if (points == null || points.Count < 3) return;

        if (complexity == ComplexityType.O1)
        {
            var cleanPoints = points.Where(p => !p.IsOutlier).ToList();
            var source = cleanPoints.Count >= 3 ? cleanPoints : points;
            var sortedTimes = source.Select(p => p.AvgMs).OrderBy(x => x).ToList();
            double globalMedian = sortedTimes[sortedTimes.Count / 2];

            // MAD
            var deviations = sortedTimes.Select(x => System.Math.Abs(x - globalMedian)).OrderBy(d => d).ToList();
            double mad = deviations[deviations.Count / 2];

            double threshold = System.Math.Max(globalMedian * 1.20, globalMedian + 2.5 * mad);

            foreach (var pt in points)
            {
                if (pt.AvgMs > threshold || pt.MedianMs > threshold || pt.Runs.Any(r => r.IsOutlier && r.ElapsedMs > 1.25 * globalMedian))
                {
                    pt.IsOutlier = true;
                }
            }
        }
        else
        {
            // Для растущих функций анализируем отклонение от тренда
            var ratios = points.Select(p => p.AvgMs / ApproximationEngine.EvaluateG(complexity, p.N)).OrderBy(x => x).ToList();
            double medianRatio = ratios[ratios.Count / 2];

            var devRatios = ratios.Select(r => System.Math.Abs(r - medianRatio)).OrderBy(d => d).ToList();
            double madRatio = devRatios[devRatios.Count / 2];

            double thresholdRatio = System.Math.Max(medianRatio * 1.30, medianRatio + 2.5 * madRatio);

            foreach (var pt in points)
            {
                double g = ApproximationEngine.EvaluateG(complexity, pt.N);
                double ratio = pt.AvgMs / g;
                if (ratio > thresholdRatio || pt.Runs.Any(r => r.IsOutlier && r.ElapsedMs > 1.35 * pt.MedianMs))
                {
                    pt.IsOutlier = true;
                }
            }
        }
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0.0;
        if (sortedValues.Count == 1) return sortedValues[0];

        double rank = percentile * (sortedValues.Count - 1);
        int low = (int)System.Math.Floor(rank);
        int high = (int)System.Math.Ceiling(rank);
        double weight = rank - low;

        return sortedValues[low] * (1.0 - weight) + sortedValues[high] * weight;
    }
}
