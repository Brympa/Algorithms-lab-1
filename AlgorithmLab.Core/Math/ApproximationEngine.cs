using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Math;

public static class ApproximationEngine
{
    public static double EvaluateG(ComplexityType complexity, double n)
    {
        if (n <= 0) n = 1;
        return complexity switch
        {
            ComplexityType.O1 => 1.0,
            ComplexityType.OLogN => System.Math.Log2(n + 1.0),
            ComplexityType.ON => n,
            ComplexityType.ONLogN => n * System.Math.Log2(n + 1.0),
            ComplexityType.ON2 => n * n,
            ComplexityType.ON3 => n * n * n,
            _ => n
        };
    }

    /// <summary>
    /// Строгий расчет коэффициента C методом наименьших квадратов:
    /// min sum (T_i - C * g_i)^2 => dS/dC = 0 => C = sum(T_i * g_i) / sum(g_i^2)
    /// </summary>
    public static ApproximationResult FitCurve(List<BenchmarkPoint> points, ComplexityType complexity)
    {
        if (points == null || points.Count == 0)
        {
            return new ApproximationResult();
        }

        // Вычисляем коэффициент C по "чистым" точкам без аномальных выбросов (если таких точек достаточно),
        // чтобы единичные паузы GC или всплески ОС не задирали теоретическую кривую вверх.
        var cleanPoints = points.Where(p => !p.IsOutlier).ToList();
        var fitSource = (cleanPoints.Count >= System.Math.Max(3, points.Count / 4)) ? cleanPoints : points;

        double sumTG = 0.0;
        double sumG2 = 0.0;
        double sumT = 0.0;

        foreach (var pt in fitSource)
        {
            double g = EvaluateG(complexity, pt.N);
            sumTG += pt.AvgMs * g;
            sumG2 += g * g;
            sumT += pt.AvgMs;
        }

        double c = (sumG2 > 1e-15) ? (sumTG / sumG2) : 0.0;
        double meanT = sumT / fitSource.Count;

        double sumSquaredErrors = 0.0;
        double totalSumSquares = 0.0;

        foreach (var pt in points)
        {
            double g = EvaluateG(complexity, pt.N);
            pt.TheoMs = c * g;
            double error = pt.AvgMs - pt.TheoMs;
            sumSquaredErrors += error * error;

            double diffMean = pt.AvgMs - meanT;
            totalSumSquares += diffMean * diffMean;
        }

        double mse = sumSquaredErrors / points.Count;
        double rmse = System.Math.Sqrt(mse);
        double rSquared = (totalSumSquares > 1e-15) ? System.Math.Max(0.0, 1.0 - (sumSquaredErrors / totalSumSquares)) : 1.0;

        string formulaDisplay = complexity switch
        {
            ComplexityType.O1 => $"T(n) = {c:E3}",
            ComplexityType.OLogN => $"T(n) = {c:E3} · log₂(n)",
            ComplexityType.ON => $"T(n) = {c:E3} · n",
            ComplexityType.ONLogN => $"T(n) = {c:E3} · n·log₂(n)",
            ComplexityType.ON2 => $"T(n) = {c:E3} · n²",
            ComplexityType.ON3 => $"T(n) = {c:E3} · n³",
            _ => $"T(n) = {c:E3} · f(n)"
        };

        double? cv = null;
        if (complexity == ComplexityType.O1)
        {
            // Для константной сложности рассчитываем коэффициент вариации CV = (sigma / mu) * 100%
            // по чистым точкам fitSource, чтобы случайные выбросы ОС не искажали реальную стабильность алгоритма
            double cleanSumSq = 0.0;
            foreach (var pt in fitSource)
            {
                double diff = pt.AvgMs - c;
                cleanSumSq += diff * diff;
            }
            double cleanSigma = System.Math.Sqrt(cleanSumSq / System.Math.Max(1, fitSource.Count));
            cv = (c > 1e-15) ? (cleanSigma / c * 100.0) : 0.0;
        }

        return new ApproximationResult
        {
            C = c,
            MSE = mse,
            RMSE = rmse,
            RSquared = rSquared,
            CV = cv,
            FormulaDisplay = formulaDisplay
        };
    }
}
