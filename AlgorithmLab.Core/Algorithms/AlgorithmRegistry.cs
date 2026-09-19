using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Algorithms;

public interface IAlgorithmRegistry
{
    IReadOnlyList<IAlgorithm> GetAll();
    IReadOnlyList<IAlgorithm> GetByCategory(AlgorithmCategory category);
    IAlgorithm? GetById(string id);
    void Register(IAlgorithm algorithm);
}

public class AlgorithmRegistry : IAlgorithmRegistry
{
    private readonly Dictionary<string, IAlgorithm> _algorithms = new(StringComparer.OrdinalIgnoreCase);

    public AlgorithmRegistry()
    {
        RegisterDefaults();
    }

    public void Register(IAlgorithm algorithm)
    {
        if (algorithm == null) throw new ArgumentNullException(nameof(algorithm));
        _algorithms[algorithm.Id] = algorithm;
    }

    public IReadOnlyList<IAlgorithm> GetAll() => _algorithms.Values.ToList();

    public IReadOnlyList<IAlgorithm> GetByCategory(AlgorithmCategory category)
    {
        return _algorithms.Values.Where(a => a.Category == category).ToList();
    }

    public IAlgorithm? GetById(string id)
    {
        return _algorithms.TryGetValue(id, out var algo) ? algo : null;
    }

    private void RegisterDefaults()
    {
        // 1. Векторные функции
        Register(new ConstantFunctionAlgorithm());
        Register(new SumFunctionAlgorithm());
        Register(new ProductFunctionAlgorithm());
        Register(new NaivePolynomialAlgorithm());
        Register(new HornerPolynomialAlgorithm());

        // 2. Сортировки
        Register(new BubbleSortAlgorithm());
        Register(new QuickSortAlgorithm());
        Register(new TimsortAlgorithm());

        // 3. Матрицы
        Register(new MatrixMultiplyAlgorithm());

        // 4. Индивидуальное задание (Часть III)
        Register(new KmpAlgorithm());
        Register(new NaiveStringSearchAlgorithm());

        // 5. Алгоритмы возведения в степень (Часть IV - подсчет шагов)
        Register(new SimpleIterativePowerAlgorithm());
        Register(new RecursivePowerAlgorithm());
        Register(new FastBinaryPowerAlgorithm());
    }
}
