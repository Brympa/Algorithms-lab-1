using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Algorithms;

public class SimpleIterativePowerAlgorithm : IStepCountableAlgorithm
{
    public string Id => "pow_simple_iterative";
    public string Name => "10. Простой итеративный: цикл из n умножений";
    public AlgorithmCategory Category => AlgorithmCategory.PowerSteps;
    public ComplexityType Complexity => ComplexityType.ON;
    public string ComplexityDisplay => "O(n) шагов";
    public string Description => "Вычисляет x^n последовательным умножением аккумулятора на x ровно n раз.";
    public string PracticalApplication => "Базовый наивный подход к возведению в степень для небольших n.";
    public int DefaultNMin => 1;
    public int DefaultNMax => 1000;
    public int DefaultStep => 10;
    public int DefaultIterations => 1;

    public StepExecutionResult ExecuteWithSteps(double x, int n)
    {
        if (n == 0) return new StepExecutionResult { Value = 1.0, StepCount = 0 };

        double f = 1.0;
        long steps = 0;
        for (int k = 0; k < n; k++)
        {
            f *= x;
            steps++; // Шаг: умножение
        }
        return new StepExecutionResult { Value = f, StepCount = steps };
    }
}

public class RecursivePowerAlgorithm : IStepCountableAlgorithm
{
    public string Id => "pow_recursive";
    public string Name => "11. Рекурсивный: x^n = x · x^{n-1}";
    public AlgorithmCategory Category => AlgorithmCategory.PowerSteps;
    public ComplexityType Complexity => ComplexityType.ON;
    public string ComplexityDisplay => "O(n) шагов";
    public string Description => "Строго по формуле x^n = x · x^{n-1}. Выполняет n рекурсивных спусков и n умножений.";
    public string PracticalApplication => "Иллюстрация линейной глубины стека вызовов и накладных расходов рекурсии.";
    public int DefaultNMin => 1;
    public int DefaultNMax => 1000;
    public int DefaultStep => 10;
    public int DefaultIterations => 1;

    public StepExecutionResult ExecuteWithSteps(double x, int n)
    {
        long stepCounter = 0;
        double val = RecPow(x, n, ref stepCounter);
        return new StepExecutionResult { Value = val, StepCount = stepCounter };
    }

    private static double RecPow(double x, int n, ref long steps)
    {
        steps++; // Шаг: рекурсивный вызов
        if (n <= 0) return 1.0;
        double res = x * RecPow(x, n - 1, ref steps);
        steps++; // Шаг: умножение при возврате
        return res;
    }
}

public class FastBinaryPowerAlgorithm : IStepCountableAlgorithm
{
    public string Id => "pow_fast_binary";
    public string Name => "12. Быстрый (бинарный): деление степени пополам";
    public AlgorithmCategory Category => AlgorithmCategory.PowerSteps;
    public ComplexityType Complexity => ComplexityType.OLogN;
    public string ComplexityDisplay => "O(log n) шагов";
    public string Description => "Бинарное возведение в степень через деление пополам x^n = (x^{n/2})^2 для четных и x · (x^{n/2})^2 для нечетных.";
    public string PracticalApplication => "Криптография с открытым ключом (RSA, Диффи-Хеллман), работа с числами произвольной точности.";
    public int DefaultNMin => 1;
    public int DefaultNMax => 1000;
    public int DefaultStep => 10;
    public int DefaultIterations => 1;

    public StepExecutionResult ExecuteWithSteps(double x, int n)
    {
        long steps = 0;
        double val = FastPow(x, n, ref steps);
        return new StepExecutionResult { Value = val, StepCount = steps };
    }

    private static double FastPow(double x, int n, ref long steps)
    {
        steps++; // Шаг: вызов
        if (n <= 0) return 1.0;

        if (n % 2 == 0)
        {
            double half = FastPow(x, n / 2, ref steps);
            steps++; // Шаг: возведение в квадрат (half * half)
            return half * half;
        }
        else
        {
            double half = FastPow(x, n / 2, ref steps);
            steps += 2; // Шаги: возведение в квадрат (half * half) и умножение на x
            return x * half * half;
        }
    }
}
