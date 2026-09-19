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

        double sumTG = 0.0;
        double sumG2 = 0.0;
        double sumT = 0.0;

        foreach (var pt in points)
        {
            double g = EvaluateG(complexity, pt.N);
            sumTG += pt.AvgMs * g;
            sumG2 += g * g;
            sumT += pt.AvgMs;
        }

        double c = (sumG2 > 1e-15) ? (sumTG / sumG2) : 0.0;
        double meanT = sumT / points.Count;

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

        return new ApproximationResult
        {
            C = c,
            MSE = mse,
            RMSE = rmse,
            RSquared = rSquared,
            FormulaDisplay = formulaDisplay
        };
    }
}
