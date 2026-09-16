namespace AlgorithmLab.Benchmark;

public enum ComplexityType
{
    O1,
    OLogN,
    ON,
    ONLogN,
    ON2,
    ON3
}

public class BenchResult
{
    public int N { get; set; }
    public double AvgMs { get; set; }  // Экспериментальное машинное время в мс
    public double TheoMs { get; set; } // Теоретическая аппроксимация в мс
}

public static class Algorithms
{
    // 1. Векторные функции
    public static readonly Dictionary<string, (Func<double[], double> Fn, ComplexityType Complexity)> VectorFunctions = new()
    {
        ["f(v) = 1 (постоянная)"] = (v => 1.0, ComplexityType.O1),
        ["Сумма элементов (Sum)"] = (v => { double s = 0; foreach (var x in v) s += x; return s; }, ComplexityType.ON),
        ["Произведение элементов (Prod)"] = (v => { double p = 1; foreach (var x in v) p *= x; return p; }, ComplexityType.ON),
        ["Полином (наивный)"] = (v => {
            double r = 0;
            for (int k = 0; k < v.Length; k++)
            {
                double p = 1;
                for (int i = 0; i < k; i++) p *= 1.5;
                r += v[k] * p;
            }
            return r;
        }, ComplexityType.ON2),
        ["Полином (Горнер)"] = (v => {
            double r = 0;
            for (int k = v.Length - 1; k >= 0; k--) r = r * 1.5 + v[k];
            return r;
        }, ComplexityType.ON)
    };

    // 2. Сортировки
    public static readonly Dictionary<string, (Action<double[]> Action, ComplexityType Complexity)> Sorts = new()
    {
        ["Bubble sort"] = (BubbleSort, ComplexityType.ON2),
        ["Quick sort"] = (v => QuickSort(v, 0, v.Length - 1), ComplexityType.ONLogN),
        ["Timsort (Array.Sort)"] = (v => Array.Sort(v), ComplexityType.ONLogN)
    };

    // 3. Алгоритмы возведения в степень (Pow)
    public static readonly Dictionary<string, (Func<double, int, double> Fn, ComplexityType Complexity)> PowAlgorithms = new()
    {
        ["Простое возведение (x^n)"] = (SimplePow, ComplexityType.ON),
        ["Рекурсивное (RecPow)"] = (RecPow, ComplexityType.OLogN),
        ["Быстрое (QuickPow)"] = (QuickPow, ComplexityType.OLogN),
        ["Классическое быстрое (QuickPow1)"] = (QuickPow1, ComplexityType.OLogN)
    };

    // 4. Матричное умножение
    public static readonly Dictionary<string, (Action<double[,], double[,]> Action, ComplexityType Complexity)> MatrixAlgorithms = new()
    {
        ["Умножение матриц n×n"] = (MatrixMultiply, ComplexityType.ON3)
    };

    // Реализация сортировок
    static void BubbleSort(double[] v)
    {
        for (int i = 0; i < v.Length - 1; i++)
            for (int j = 0; j < v.Length - i - 1; j++)
                if (v[j] > v[j + 1]) (v[j], v[j + 1]) = (v[j + 1], v[j]);
    }

    static void QuickSort(double[] v, int l, int r)
    {
        if (l >= r) return;
        int p = Partition(v, l, r);
        QuickSort(v, l, p - 1);
        QuickSort(v, p + 1, r);
    }

    static int Partition(double[] v, int l, int r)
    {
        double pivot = v[r];
        int i = l - 1;
        for (int j = l; j < r; j++)
            if (v[j] < pivot) (v[++i], v[j]) = (v[j], v[i]);
        (v[i + 1], v[r]) = (v[r], v[i + 1]);
        return i + 1;
    }

    // Реализация Pow
    static double SimplePow(double x, int n)
    {
        double f = 1;
        for (int k = 0; k < n; k++) f *= x;
        return f;
    }

    static double RecPow(double x, int n)
    {
        if (n == 0) return 1;
        double f = RecPow(x, n / 2);
        if (n % 2 == 1) return f * f * x;
        else return f * f;
    }

    static double QuickPow(double x, int n)
    {
        double c = x;
        int k = n;
        double f = (k % 2 == 1) ? c : 1;
        while (k > 0)
        {
            k /= 2;
            c = c * c;
            if (k % 2 == 1) f = f * c;
        }
        return f;
    }

    static double QuickPow1(double x, int n)
    {
        double c = x;
        double f = 1;
        int k = n;
        while (k > 0)
        {
            if (k % 2 == 0)
            {
                c = c * c;
                k /= 2;
            }
            else
            {
                f = f * c;
                k -= 1;
            }
        }
        return f;
    }

    // Реализация умножения матриц
    static void MatrixMultiply(double[,] A, double[,] B)
    {
        int n = A.GetLength(0);
        double[,] C = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                double s = 0;
                for (int k = 0; k < n; k++) s += A[i, k] * B[k, j];
                C[i, j] = s;
            }
    }
}