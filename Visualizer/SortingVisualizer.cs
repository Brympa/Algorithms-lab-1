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
            case "Selection Sort":
                RunSelectionSort(arr, AddStep, ref comparisons, ref swaps, sortedIndices);
                break;
            case "Insertion Sort":
                RunInsertionSort(arr, AddStep, ref comparisons, ref swaps, sortedIndices);
                break;
            case "Quick Sort":
                RunQuickSort(arr, 0, arr.Length - 1, AddStep, ref comparisons, ref swaps, sortedIndices);
                for (int i = 0; i < arr.Length; i++) sortedIndices.Add(i);
                AddStep(msg: "Сортировка завершена!");
                break;
            case "Merge Sort":
                RunMergeSort(arr, 0, arr.Length - 1, AddStep, ref comparisons, ref swaps, sortedIndices);
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

    private static void RunSelectionSort(int[] arr, StepLogger addStep, ref int comparisons, ref int swaps, HashSet<int> sortedIndices)
    {
        int n = arr.Length;
        for (int i = 0; i < n - 1; i++)
        {
            int minIdx = i;
            addStep(comp1: minIdx, pivot: minIdx, msg: $"Поиск минимума начиная с индекса {i}");

            for (int j = i + 1; j < n; j++)
            {
                comparisons++;
                addStep(comp1: j, comp2: minIdx, pivot: minIdx, msg: $"Сравнение {arr[j]} с текущим минимумом {arr[minIdx]}");

                if (arr[j] < arr[minIdx])
                {
                    minIdx = j;
                    addStep(comp1: j, pivot: minIdx, msg: $"Найден новый минимум: {arr[minIdx]}");
                }
            }

            if (minIdx != i)
            {
                (arr[i], arr[minIdx]) = (arr[minIdx], arr[i]);
                swaps++;
                addStep(swap1: i, swap2: minIdx, msg: $"Обмен минимума {arr[i]} на позицию {i}");
            }
            sortedIndices.Add(i);
        }
        sortedIndices.Add(n - 1);
        addStep(msg: "Сортировка завершена!");
    }

    private static void RunInsertionSort(int[] arr, StepLogger addStep, ref int comparisons, ref int swaps, HashSet<int> sortedIndices)
    {
        int n = arr.Length;
        sortedIndices.Add(0);

        for (int i = 1; i < n; i++)
        {
            int key = arr[i];
            int j = i - 1;
            addStep(comp1: i, pivot: i, msg: $"Вставка элемента {key} в отсортированную часть");

            while (j >= 0 && arr[j] > key)
            {
                comparisons++;
                arr[j + 1] = arr[j];
                swaps++;
                addStep(swap1: j, swap2: j + 1, msg: $"Сдвиг элемента {arr[j]} вправо");
                j--;
            }
            if (j >= 0) comparisons++;

            arr[j + 1] = key;
            swaps++;
            sortedIndices.Add(i);
            addStep(swap1: j + 1, msg: $"Размещение {key} на позиции {j + 1}");
        }
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

    private static void RunMergeSort(int[] arr, int left, int right, StepLogger addStep, ref int comparisons, ref int swaps, HashSet<int> sortedIndices)
    {
        if (left < right)
        {
            int middle = left + (right - left) / 2;
            RunMergeSort(arr, left, middle, addStep, ref comparisons, ref swaps, sortedIndices);
            RunMergeSort(arr, middle + 1, right, addStep, ref comparisons, ref swaps, sortedIndices);
            Merge(arr, left, middle, right, addStep, ref comparisons, ref swaps);
        }
    }

    private static void Merge(int[] arr, int left, int middle, int right, StepLogger addStep, ref int comparisons, ref int swaps)
    {
        int n1 = middle - left + 1;
        int n2 = right - middle;

        int[] L = new int[n1];
        int[] R = new int[n2];

        Array.Copy(arr, left, L, 0, n1);
        Array.Copy(arr, middle + 1, R, 0, n2);

        int i = 0, j = 0;
        int k = left;

        addStep(comp1: left, comp2: right, msg: $"Слияние подмассивов [{left}..{middle}] и [{middle + 1}..{right}]");

        while (i < n1 && j < n2)
        {
            comparisons++;
            addStep(comp1: left + i, comp2: middle + 1 + j, msg: $"Сравнение элементов {L[i]} и {R[j]}");

            if (L[i] <= R[j])
            {
                arr[k] = L[i];
                swaps++;
                addStep(swap1: k, msg: $"Запись {L[i]} в массив на позицию {k}");
                i++;
            }
            else
            {
                arr[k] = R[j];
                swaps++;
                addStep(swap1: k, msg: $"Запись {R[j]} в массив на позицию {k}");
                j++;
            }
            k++;
        }

        while (i < n1)
        {
            arr[k] = L[i];
            swaps++;
            addStep(swap1: k, msg: $"Запись оставшегося элемента {L[i]} на позицию {k}");
            i++;
            k++;
        }

        while (j < n2)
        {
            arr[k] = R[j];
            swaps++;
            addStep(swap1: k, msg: $"Запись оставшегося элемента {R[j]} на позицию {k}");
            j++;
            k++;
        }
    }
}
