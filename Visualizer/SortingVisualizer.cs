namespace AlgorithmLab.Visualizer;

public delegate void StepLogger(int comp1 = -1, int comp2 = -1, int swap1 = -1, int swap2 = -1, int pivot = -1, string msg = "");

public static class SortingVisualizer
{
    public static List<StepSnapshot> GenerateSteps(string algorithm, int[] inputArr)
    {
        var steps = new List<StepSnapshot>();
        int[] arr = (int[])inputArr.Clone();
        int comparisons = 0;
        int swaps = 0;
        var sortedIndices = new HashSet<int>();

        void AddStep(int comp1 = -1, int comp2 = -1, int swap1 = -1, int swap2 = -1, int pivot = -1, string msg = "")
        {
            steps.Add(new StepSnapshot
            {
                Array = (int[])arr.Clone(),
                ComparingIndex1 = comp1,
                ComparingIndex2 = comp2,
                SwappingIndex1 = swap1,
                SwappingIndex2 = swap2,
                PivotIndex = pivot,
                SortedIndices = new HashSet<int>(sortedIndices),
                Comparisons = comparisons,
                Swaps = swaps,
                Message = msg
            });
        }

        AddStep(msg: "Исходный массив");

        switch (algorithm)
        {
            case "Bubble Sort":
                RunBubbleSort(arr, AddStep, ref comparisons, ref swaps, sortedIndices);
                break;
            case "Quick Sort":
                RunQuickSort(arr, 0, arr.Length - 1, AddStep, ref comparisons, ref swaps, sortedIndices);
                for (int i = 0; i < arr.Length; i++) sortedIndices.Add(i);
                AddStep(msg: "Сортировка завершена!");
                break;
            case "Pancake Sort":
                RunPancakeSort(arr, AddStep, ref comparisons, ref swaps, sortedIndices);
                for (int i = 0; i < arr.Length; i++) sortedIndices.Add(i);
                AddStep(msg: "Сортировка завершена!");
                break;
            case "Cocktail Shaker Sort":
                RunCocktailShakerSort(arr, AddStep, ref comparisons, ref swaps, sortedIndices);
                for (int i = 0; i < arr.Length; i++) sortedIndices.Add(i);
                AddStep(msg: "Сортировка завершена!");
                break;
            case "Comb Sort":
                RunCombSort(arr, AddStep, ref comparisons, ref swaps, sortedIndices);
                for (int i = 0; i < arr.Length; i++) sortedIndices.Add(i);
                AddStep(msg: "Сортировка завершена!");
                break;
            default:
                RunBubbleSort(arr, AddStep, ref comparisons, ref swaps, sortedIndices);
                break;
        }

        return steps;
    }

    private static void RunBubbleSort(int[] arr, StepLogger addStep, ref int comparisons, ref int swaps, HashSet<int> sortedIndices)
    {
        int n = arr.Length;
        for (int i = 0; i < n - 1; i++)
        {
            for (int j = 0; j < n - i - 1; j++)
            {
                comparisons++;
                addStep(j, j + 1, msg: $"Сравнение элементов {arr[j]} и {arr[j + 1]}");

                if (arr[j] > arr[j + 1])
                {
                    (arr[j], arr[j + 1]) = (arr[j + 1], arr[j]);
                    swaps++;
                    addStep(swap1: j, swap2: j + 1, msg: $"Обмен мест: {arr[j + 1]} и {arr[j]}");
                }
            }
            sortedIndices.Add(n - 1 - i);
            addStep(msg: $"Элемент {arr[n - 1 - i]} занял финальную позицию");
        }
        sortedIndices.Add(0);
        addStep(msg: "Сортировка завершена!");
    }

    private static void RunQuickSort(int[] arr, int low, int high, StepLogger addStep, ref int comparisons, ref int swaps, HashSet<int> sortedIndices)
    {
        if (low < high)
        {
            int pi = Partition(arr, low, high, addStep, ref comparisons, ref swaps);
            sortedIndices.Add(pi);
            addStep(msg: $"Элемент {arr[pi]} зафиксирован на позиции {pi}");

            RunQuickSort(arr, low, pi - 1, addStep, ref comparisons, ref swaps, sortedIndices);
            RunQuickSort(arr, pi + 1, high, addStep, ref comparisons, ref swaps, sortedIndices);
        }
        else if (low == high)
        {
            sortedIndices.Add(low);
        }
    }

    private static int Partition(int[] arr, int low, int high, StepLogger addStep, ref int comparisons, ref int swaps)
    {
        int pivot = arr[high];
        addStep(pivot: high, msg: $"Выбран опорный элемент (Pivot): {pivot}");
        int i = low - 1;

        for (int j = low; j < high; j++)
        {
            comparisons++;
            addStep(comp1: j, comp2: high, pivot: high, msg: $"Сравнение {arr[j]} с опорным {pivot}");

            if (arr[j] < pivot)
            {
                i++;
                if (i != j)
                {
                    (arr[i], arr[j]) = (arr[j], arr[i]);
                    swaps++;
                    addStep(swap1: i, swap2: j, pivot: high, msg: $"Перестановка {arr[i]} и {arr[j]}");
                }
            }
        }

        (arr[i + 1], arr[high]) = (arr[high], arr[i + 1]);
        swaps++;
        addStep(swap1: i + 1, swap2: high, pivot: i + 1, msg: $"Размещение опорного {pivot} на итоговую позицию {i + 1}");
        return i + 1;
    }

    private static void RunPancakeSort(int[] arr, StepLogger addStep, ref int comparisons, ref int swaps, HashSet<int> sortedIndices)
    {
        int n = arr.Length;
        for (int currSize = n; currSize > 1; currSize--)
        {
            int maxIdx = 0;
            for (int i = 1; i < currSize; i++)
            {
                comparisons++;
                addStep(comp1: i, comp2: maxIdx, msg: $"Поиск максимума в стопке [0..{currSize - 1}]: сравниваем {arr[i]} и {arr[maxIdx]}");
                if (arr[i] > arr[maxIdx])
                {
                    maxIdx = i;
                }
            }

            if (maxIdx != currSize - 1)
            {
                // Переворачиваем префикс до maxIdx, чтобы вытащить максимум наверх стопки
                if (maxIdx > 0)
                {
                    FlipWithSteps(arr, maxIdx, addStep, ref swaps, $"Флип префикса [0..{maxIdx}]: перемещаем максимум {arr[maxIdx]} на вершину");
                }
                // Переворачиваем всю стопку до currSize - 1, чтобы поместить максимум на его место
                FlipWithSteps(arr, currSize - 1, addStep, ref swaps, $"Флип всей текущей стопки [0..{currSize - 1}]: отправляем максимум вниз");
            }
            sortedIndices.Add(currSize - 1);
            addStep(msg: $"Элемент {arr[currSize - 1]} зафиксирован на позиции {currSize - 1}");
        }
    }

    private static void FlipWithSteps(int[] arr, int k, StepLogger addStep, ref int swaps, string msg)
    {
        int left = 0;
        int right = k;
        while (left < right)
        {
            (arr[left], arr[right]) = (arr[right], arr[left]);
            swaps++;
            addStep(swap1: left, swap2: right, msg: msg);
            left++;
            right--;
        }
    }

    private static void RunCocktailShakerSort(int[] arr, StepLogger addStep, ref int comparisons, ref int swaps, HashSet<int> sortedIndices)
    {
        int n = arr.Length;
        if (n <= 1) return;

        bool swapped = true;
        int start = 0;
        int end = n - 1;

        while (swapped)
        {
            swapped = false;

            // Проход слева направо (тяжелые элементы)
            for (int i = start; i < end; i++)
            {
                comparisons++;
                addStep(comp1: i, comp2: i + 1, msg: $"Проход вправо: сравнение {arr[i]} и {arr[i + 1]}");

                if (arr[i] > arr[i + 1])
                {
                    (arr[i], arr[i + 1]) = (arr[i + 1], arr[i]);
                    swaps++;
                    swapped = true;
                    addStep(swap1: i, swap2: i + 1, msg: $"Обмен мест: {arr[i + 1]} и {arr[i]}");
                }
            }

            sortedIndices.Add(end);
            addStep(msg: $"Элемент {arr[end]} зафиксирован на позиции {end}");
            if (!swapped) break;

            swapped = false;
            end--;

            // Проход справа налево (легкие элементы)
            for (int i = end - 1; i >= start; i--)
            {
                comparisons++;
                addStep(comp1: i, comp2: i + 1, msg: $"Проход влево: сравнение {arr[i]} и {arr[i + 1]}");

                if (arr[i] > arr[i + 1])
                {
                    (arr[i], arr[i + 1]) = (arr[i + 1], arr[i]);
                    swaps++;
                    swapped = true;
                    addStep(swap1: i, swap2: i + 1, msg: $"Обмен мест: {arr[i + 1]} и {arr[i]}");
                }
            }

            sortedIndices.Add(start);
            addStep(msg: $"Элемент {arr[start]} зафиксирован на позиции {start}");
            start++;
        }
    }

    private static void RunCombSort(int[] arr, StepLogger addStep, ref int comparisons, ref int swaps, HashSet<int> sortedIndices)
    {
        int n = arr.Length;
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
                gap = 11; // Rule 11
            }

            addStep(msg: $"Текущий шаг расчески gap = {gap}");

            for (int i = 0; i + gap < n; i++)
            {
                comparisons++;
                addStep(comp1: i, comp2: i + gap, msg: $"Сравнение элементов на расстоянии {gap}: {arr[i]} и {arr[i + gap]}");

                if (arr[i] > arr[i + gap])
                {
                    (arr[i], arr[i + gap]) = (arr[i + gap], arr[i]);
                    swaps++;
                    sorted = false;
                    addStep(swap1: i, swap2: i + gap, msg: $"Обмен на расстоянии {gap}: {arr[i + gap]} и {arr[i]}");
                }
            }
        }
    }
}
