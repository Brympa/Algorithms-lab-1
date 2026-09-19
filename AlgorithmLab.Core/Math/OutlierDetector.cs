using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Math;

public static class OutlierDetector
{
    /// <summary>
    /// Детекция выбросов внутри набора независимых прогонов для одной точки N
    /// с использованием критерия Тьюки (IQR) и Z-score.
    /// </summary>
    public static void DetectRunOutliers(List<PointRun> runs)
    {
        if (runs == null || runs.Count < 3) return;

        var sorted = runs.OrderBy(r => r.ElapsedMs).ToList();
        double q1 = GetPercentile(sorted.Select(r => r.ElapsedMs).ToList(), 0.25);
        double q3 = GetPercentile(sorted.Select(r => r.ElapsedMs).ToList(), 0.75);
        double iqr = q3 - q1;

        double lowerBound = q1 - 1.5 * iqr;
        double upperBound = q3 + 1.5 * iqr;

        // Если IQR слишком мал (микросекундные замеры), используем Z-score
        double mean = runs.Average(r => r.ElapsedMs);
        double variance = runs.Sum(r => (r.ElapsedMs - mean) * (r.ElapsedMs - mean)) / runs.Count;
        double stdDev = System.Math.Sqrt(variance);

        foreach (var run in runs)
        {
            bool isIqrOutlier = (iqr > 1e-6) && (run.ElapsedMs < lowerBound || run.ElapsedMs > upperBound);
            bool isZOutlier = (stdDev > 1e-6) && (System.Math.Abs(run.ElapsedMs - mean) > 2.0 * stdDev);
            bool isSpikeOutlier = (mean > 1e-6) && (run.ElapsedMs > 2.5 * mean);

            run.IsOutlier = isIqrOutlier || isZOutlier || isSpikeOutlier;
        }
    }

    /// <summary>
    /// Детекция выбросов среди серии точек на графике
    /// (например, если среднее время резко подскочило из-за системных прерываний).
    /// </summary>
    public static void DetectPointOutliers(List<BenchmarkPoint> points)
    {
        if (points == null || points.Count < 5) return;

        // Скользящее окно медианы (размер окна 5)
        for (int i = 0; i < points.Count; i++)
        {
            int start = System.Math.Max(0, i - 2);
            int end = System.Math.Min(points.Count - 1, i + 2);
            var window = points.Skip(start).Take(end - start + 1).Select(p => p.AvgMs).OrderBy(x => x).ToList();
            double localMedian = window[window.Count / 2];

            // Если текущая точка превышает локальную медиану более чем в 2.5 раза или отклонение аномально
            if (localMedian > 1e-6 && points[i].AvgMs > 2.2 * localMedian)
            {
                points[i].IsOutlier = true;
            }
            else if (points[i].Runs.Any(r => r.IsOutlier && r.ElapsedMs > 3.0 * points[i].MedianMs))
            {
                points[i].IsOutlier = true;
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
