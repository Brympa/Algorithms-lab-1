using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Algorithms;

public class KmpAlgorithm : IStringSearchAlgorithm
{
    public string Id => "ind_kmp";
    public string Name => "Поиск подстроки Кнута-Морриса-Пратта (KMP)";
    public AlgorithmCategory Category => AlgorithmCategory.Individual;
    public ComplexityType Complexity => ComplexityType.ON;
    public string ComplexityDisplay => "O(n + m)";
    public string Description => "Линейный алгоритм поиска образца в тексте с использованием префикс-функции (π) для предотвращения возвратов по тексту.";
    public string PracticalApplication => "Биоинформатика (поиск последовательностей ДНК), текстовые редакторы, поисковые движки, сетевые фильтры пакетов.";
    public int DefaultNMin => 1000;
    public int DefaultNMax => 50000;
    public int DefaultStep => 2500;
    public int DefaultIterations => 30;

    public int Execute(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(text)) return -1;

        int[] pi = ComputePrefixFunction(pattern);
        int j = 0; // индекс в pattern

        for (int i = 0; i < text.Length; i++)
        {
            while (j > 0 && text[i] != pattern[j])
            {
                j = pi[j - 1];
            }
            if (text[i] == pattern[j])
            {
                j++;
            }
            if (j == pattern.Length)
            {
                return i - pattern.Length + 1; // Найдено первое совпадение
            }
        }
        return -1;
    }

    private static int[] ComputePrefixFunction(string pattern)
    {
        int m = pattern.Length;
        int[] pi = new int[m];
        int j = 0;

        for (int i = 1; i < m; i++)
        {
            while (j > 0 && pattern[i] != pattern[j])
            {
                j = pi[j - 1];
            }
            if (pattern[i] == pattern[j])
            {
                j++;
            }
            pi[i] = j;
        }
        return pi;
    }
}

public class NaiveStringSearchAlgorithm : IStringSearchAlgorithm
{
    public string Id => "ind_naive_search";
    public string Name => "Наивный поиск подстроки";
    public AlgorithmCategory Category => AlgorithmCategory.Individual;
    public ComplexityType Complexity => ComplexityType.ON2;
    public string ComplexityDisplay => "O(n · m)";
    public string Description => "Прямой перебор всех возможных позиций начала образца с посимвольным сравнением.";
    public string PracticalApplication => "Сравнение эффективности с оптимальным алгоритмом KMP на худших случаях (периодические строки).";
    public int DefaultNMin => 200;
    public int DefaultNMax => 5000;
    public int DefaultStep => 200;
    public int DefaultIterations => 10;

    public int Execute(string text, string pattern)
    {
        int n = text.Length;
        int m = pattern.Length;

        for (int i = 0; i <= n - m; i++)
        {
            int j = 0;
            while (j < m && text[i + j] == pattern[j])
            {
                j++;
            }
            if (j == m) return i;
        }
        return -1;
    }
}
