using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Algorithms;

public class BubbleSortAlgorithm : ISortAlgorithm
{
    public string Id => "sort_bubble";
    public string Name => "Сортировка пузырьком (Bubble sort)";
    public AlgorithmCategory Category => AlgorithmCategory.Sorts;
    public ComplexityType Complexity => ComplexityType.ON2;
    public string ComplexityDisplay => "O(n²)";
    public string Description => "Классическая обменная сортировка: сравнивает соседние элементы и меняет их местами.";
    public string PracticalApplication => "Обучение алгоритмам, быстрая проверка почти отсортированных коротких массивов.";
    public int DefaultNMin => 20;
    public int DefaultNMax => 100_000; // Стартовое; жёсткого потолка в UI нет
    public int DefaultStep => 500;
    public int DefaultIterations => 5;

    public void Execute(double[] array)
    {
        int n = array.Length;
        for (int i = 0; i < n - 1; i++)
        {
            bool swapped = false;
            for (int j = 0; j < n - i - 1; j++)
            {
                if (array[j] > array[j + 1])
                {
                    (array[j], array[j + 1]) = (array[j + 1], array[j]);
                    swapped = true;
                }
            }
            if (!swapped) break;
        }
    }
}

public class QuickSortAlgorithm : ISortAlgorithm
{
    public string Id => "sort_quick";
    public string Name => "Быстрая сортировка (Quick sort)";
    public AlgorithmCategory Category => AlgorithmCategory.Sorts;
    public ComplexityType Complexity => ComplexityType.ONLogN;
    public string ComplexityDisplay => "O(n log n)";
    public string Description => "Рекурсивный алгоритм 'разделяй и властвуй' с выбором опорного элемента (Pivot) и разбиением Ломуто/Хоара.";
    public string PracticalApplication => "Универсальная эффективная сортировка общего назначения в высокопроизводительных движках.";
    public int DefaultNMin => 50;
    public int DefaultNMax => 1_000_000;
    public int DefaultStep => 5000;
    public int DefaultIterations => 10;

    public void Execute(double[] array)
    {
        if (array.Length > 1)
        {
            QuickSortInternal(array, 0, array.Length - 1);
        }
    }

    private static void QuickSortInternal(double[] arr, int low, int high)
    {
        if (low < high)
        {
            int p = Partition(arr, low, high);
            QuickSortInternal(arr, low, p - 1);
            QuickSortInternal(arr, p + 1, high);
        }
    }

    private static int Partition(double[] arr, int low, int high)
    {
        // Выбор медианы из трех для предотвращения худшего случая O(n^2) на отсортированных данных
        int mid = low + (high - low) / 2;
        if (arr[mid] < arr[low]) (arr[low], arr[mid]) = (arr[mid], arr[low]);
        if (arr[high] < arr[low]) (arr[low], arr[high]) = (arr[high], arr[low]);
        if (arr[mid] < arr[high]) (arr[mid], arr[high]) = (arr[high], arr[mid]);

        double pivot = arr[high];
        int i = low - 1;

        for (int j = low; j < high; j++)
        {
            if (arr[j] <= pivot)
            {
                i++;
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }
        (arr[i + 1], arr[high]) = (arr[high], arr[i + 1]);
        return i + 1;
    }
}

public class TimsortAlgorithm : ISortAlgorithm
{
    public string Id => "sort_timsort";
    public string Name => "Гибридная сортировка (Timsort / IntroSort)";
    public AlgorithmCategory Category => AlgorithmCategory.Sorts;
    public ComplexityType Complexity => ComplexityType.ONLogN;
    public string ComplexityDisplay => "O(n log n)";
    public string Description => "Стандартная гибридная оптимизированная сортировка платформы .NET (IntroSort: QuickSort + HeapSort + InsertionSort).";
    public string PracticalApplication => "Используется по умолчанию в стандартных библиотеках языков программирования (Array.Sort, List.Sort).";
    public int DefaultNMin => 50;
    public int DefaultNMax => 1_000_000;
    public int DefaultStep => 5000;
    public int DefaultIterations => 10;

    public void Execute(double[] array)
    {
        Array.Sort(array);
    }
}
