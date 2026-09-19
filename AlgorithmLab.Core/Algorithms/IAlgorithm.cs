using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Algorithms;

public interface IAlgorithm
{
    string Id { get; }
    string Name { get; }
    AlgorithmCategory Category { get; }
    ComplexityType Complexity { get; }
    string ComplexityDisplay { get; }
    string Description { get; }
    string PracticalApplication { get; }
    int DefaultNMin { get; }
    int DefaultNMax { get; }
    int DefaultStep { get; }
    int DefaultIterations { get; }
}

public interface IVectorAlgorithm : IAlgorithm
{
    double Execute(double[] vector);
}

public interface ISortAlgorithm : IAlgorithm
{
    void Execute(double[] array);
}

public interface IMatrixAlgorithm : IAlgorithm
{
    double[,] Execute(double[,] a, double[,] b);
}

public interface IStepCountableAlgorithm : IAlgorithm
{
    StepExecutionResult ExecuteWithSteps(double x, int n);
}

public interface IStringSearchAlgorithm : IAlgorithm
{
    int Execute(string text, string pattern);
}
