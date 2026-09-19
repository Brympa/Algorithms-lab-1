using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Algorithms;

public class ConstantFunctionAlgorithm : IVectorAlgorithm
{
    public string Id => "vec_const";
    public string Name => "Постоянная функция: f(v) = 1";
    public AlgorithmCategory Category => AlgorithmCategory.Vectors;
    public ComplexityType Complexity => ComplexityType.O1;
    public string ComplexityDisplay => "O(1)";
    public string Description => "Возвращает константное значение 1.0 независимо от размера вектора.";
    public string PracticalApplication => "Используется для оценки базовых накладных расходов вызова функции и доступа к объекту.";
    public int DefaultNMin => 1000;
    public int DefaultNMax => 50000;
    public int DefaultStep => 2500;
    public int DefaultIterations => 200;

    public double Execute(double[] vector)
    {
        return 1.0;
    }
}

public class SumFunctionAlgorithm : IVectorAlgorithm
{
    public string Id => "vec_sum";
    public string Name => "Сумма элементов: f(v) = ∑ v_k";
    public AlgorithmCategory Category => AlgorithmCategory.Vectors;
    public ComplexityType Complexity => ComplexityType.ON;
    public string ComplexityDisplay => "O(n)";
    public string Description => "Вычисляет сумму всех элементов вектора линейным проходом.";
    public string PracticalApplication => "Базовая операция статистического анализа, вычисления среднего значения, агрегации данных.";
    public int DefaultNMin => 100;
    public int DefaultNMax => 10000;
    public int DefaultStep => 500;
    public int DefaultIterations => 50;

    public double Execute(double[] vector)
    {
        double sum = 0.0;
        for (int i = 0; i < vector.Length; i++)
        {
            sum += vector[i];
        }
        return sum;
    }
}

public class ProductFunctionAlgorithm : IVectorAlgorithm
{
    public string Id => "vec_prod";
    public string Name => "Произведение элементов: f(v) = ∏ v_k";
    public AlgorithmCategory Category => AlgorithmCategory.Vectors;
    public ComplexityType Complexity => ComplexityType.ON;
    public string ComplexityDisplay => "O(n)";
    public string Description => "Вычисляет произведение элементов вектора. Для предотвращения переполнения нормализуется.";
    public string PracticalApplication => "Геометрическое среднее, вычисление вероятностей независимых событий.";
    public int DefaultNMin => 100;
    public int DefaultNMax => 10000;
    public int DefaultStep => 500;
    public int DefaultIterations => 50;

    public double Execute(double[] vector)
    {
        double prod = 1.0;
        for (int i = 0; i < vector.Length; i++)
        {
            prod *= (vector[i] * 0.01 + 0.99); // Масштабирование во избежание бесконечности/NaN
        }
        return prod;
    }
}

public class NaivePolynomialAlgorithm : IVectorAlgorithm
{
    private readonly double _x;

    public NaivePolynomialAlgorithm(double x = 1.5)
    {
        _x = x;
    }

    public string Id => "vec_poly_naive";
    public string Name => "Полином (наивный расчет степени): P(x)";
    public AlgorithmCategory Category => AlgorithmCategory.Vectors;
    public ComplexityType Complexity => ComplexityType.ON2;
    public string ComplexityDisplay => "O(n²)";
    public string Description => "Вычисляет P(x) = ∑ v_k · x^{k-1}, явно вычисляя степень x^{k-1} вложенным циклом для каждого слагаемого.";
    public string PracticalApplication => "Демонстрация квадратичной неэффективности прямолинейной математической реализации.";
    public int DefaultNMin => 20;
    public int DefaultNMax => 400; // Откалибровано по ТЗ, чтобы не вешать браузер
    public int DefaultStep => 15;
    public int DefaultIterations => 10;

    public double Execute(double[] vector)
    {
        double result = 0.0;
        for (int k = 0; k < vector.Length; k++)
        {
            double power = 1.0;
            for (int i = 0; i < k; i++)
            {
                power *= _x;
            }
            result += vector[k] * power;
        }
        return result;
    }
}

public class HornerPolynomialAlgorithm : IVectorAlgorithm
{
    private readonly double _x;

    public HornerPolynomialAlgorithm(double x = 1.5)
    {
        _x = x;
    }

    public string Id => "vec_poly_horner";
    public string Name => "Полином (схема Горнера): P(x)";
    public AlgorithmCategory Category => AlgorithmCategory.Vectors;
    public ComplexityType Complexity => ComplexityType.ON;
    public string ComplexityDisplay => "O(n)";
    public string Description => "Вычисляет значение многочлена по схеме Горнера: v_1 + x(v_2 + x(v_3 + ...)).";
    public string PracticalApplication => "Стандарт вычисления полиномов в математических сопроцессорах, интерполяции и CAD-системах.";
    public int DefaultNMin => 100;
    public int DefaultNMax => 10000;
    public int DefaultStep => 500;
    public int DefaultIterations => 50;

    public double Execute(double[] vector)
    {
        double result = 0.0;
        for (int k = vector.Length - 1; k >= 0; k--)
        {
            result = result * _x + vector[k];
        }
        return result;
    }
}
