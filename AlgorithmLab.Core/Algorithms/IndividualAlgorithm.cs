using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Algorithms;

/// <summary>
/// Индивидуальное задание (Часть III ТЗ) - Даша
/// Блинная сортировка: переворачивание префиксов массива
/// </summary>
public class PancakeSortAlgorithm : ISortAlgorithm
{
    public string Id => "ind_pancake";
    public string Name => "Блинная сортировка (Pancake sort)";
    public AlgorithmCategory Category => AlgorithmCategory.Individual;
    public ComplexityType Complexity => ComplexityType.ON2;
    public string ComplexityDisplay => "O(n²)";
    public string Description => "Сортировка стопки элементов путем последовательных переворотов (flips) префиксов массива от 0 до k для перемещения очередного максимума в конец.";
    public string PracticalApplication => "Генетические алгоритмы (анализ мутаций хромосом), маршрутизация пакетов в сетях с топологией графов Кэли, робототехника.";
    public int DefaultNMin => 20;
    public int DefaultNMax => 400; // Для O(n^2) по ТЗ во избежание зависания
    public int DefaultStep => 15;
    public int DefaultIterations => 5;

    public void Execute(double[] array)
    {
        int n = array.Length;
        for (int currSize = n; currSize > 1; currSize--)
        {
            int maxIdx = 0;
            for (int i = 1; i < currSize; i++)
            {
                if (array[i] > array[maxIdx])
                {
                    maxIdx = i;
                }
            }

            if (maxIdx != currSize - 1)
            {
                if (maxIdx > 0)
                {
                    Flip(array, maxIdx);
                }
                Flip(array, currSize - 1);
            }
        }
    }

    private static void Flip(double[] arr, int k)
    {
        int left = 0;
        int right = k;
        while (left < right)
        {
            (arr[left], arr[right]) = (arr[right], arr[left]);
            left++;
            right--;
        }
    }
}

/// <summary>
/// Индивидуальное задание (Часть III ТЗ) - Вадик
/// Шейкерная сортировка: двунаправленный пузырек
/// </summary>
public class CocktailShakerSortAlgorithm : ISortAlgorithm
{
    public string Id => "ind_cocktail_shaker";
    public string Name => "Шейкерная сортировка (Cocktail shaker sort)";
    public AlgorithmCategory Category => AlgorithmCategory.Individual;
    public ComplexityType Complexity => ComplexityType.ON2;
    public string ComplexityDisplay => "O(n²)";
    public string Description => "Двунаправленная пузырьковая сортировка: поочередные проходы слева направо (всплывание максимума) и справа налево (опускание минимума).";
    public string PracticalApplication => "Эффективна на частично упорядоченных массивах, решает проблему «черепах» (малых элементов в конце), контроллеры реального времени.";
    public int DefaultNMin => 20;
    public int DefaultNMax => 400; // Для O(n^2) по ТЗ
    public int DefaultStep => 15;
    public int DefaultIterations => 5;

    public void Execute(double[] array)
    {
        int n = array.Length;
        if (n <= 1) return;

        bool swapped = true;
        int start = 0;
        int end = n - 1;

        while (swapped)
        {
            swapped = false;

            // Проход слева направо (тяжелые элементы всплывают вправо)
            for (int i = start; i < end; i++)
            {
                if (array[i] > array[i + 1])
                {
                    (array[i], array[i + 1]) = (array[i + 1], array[i]);
                    swapped = true;
                }
            }

            if (!swapped) break;

            swapped = false;
            end--;

            // Проход справа налево (легкие элементы опускаются влево)
            for (int i = end - 1; i >= start; i--)
            {
                if (array[i] > array[i + 1])
                {
                    (array[i], array[i + 1]) = (array[i + 1], array[i]);
                    swapped = true;
                }
            }

            start++;
        }
    }
}

/// <summary>
/// Индивидуальное задание (Часть III ТЗ) - Илья
/// Сортировка расчёской: устранение инверсий с уменьшающимся шагом (фактор 1.3)
/// </summary>
public class CombSortAlgorithm : ISortAlgorithm
{
    public string Id => "ind_comb";
    public string Name => "Сортировка расчёской (Comb sort)";
    public AlgorithmCategory Category => AlgorithmCategory.Individual;
    public ComplexityType Complexity => ComplexityType.ONLogN;
    public string ComplexityDisplay => "O(n log n)";
    public string Description => "Улучшение пузырьковой сортировки: сравнивает элементы на расстоянии шага gap, уменьшающегося с фактором сжатия 1.3 (shrink factor) с правилом Rule 11.";
    public string PracticalApplication => "Высокоскоростная сортировка без накладных расходов на память O(1) и стек рекурсии в низкоуровневых драйверах и сетевых устройствах.";
    public int DefaultNMin => 50;
    public int DefaultNMax => 2000;
    public int DefaultStep => 50;
    public int DefaultIterations => 10;

    public void Execute(double[] array)
    {
        int n = array.Length;
        if (n <= 1) return;

        int gap = n;
        const double shrink = 1.3;
        bool sorted = false;

        while (!sorted)
        {
            gap = (int)(gap / shrink);
            if (gap <= 1)
            {
                gap = 1;
                sorted = true;
            }
            else if (gap == 9 || gap == 10)
            {
                gap = 11; // Эмпирическое правило Rule 11 ускоряет сходимость
            }

            for (int i = 0; i + gap < n; i++)
            {
                if (array[i] > array[i + gap])
                {
                    (array[i], array[i + gap]) = (array[i + gap], array[i]);
                    sorted = false;
                }
            }
        }
    }
}
