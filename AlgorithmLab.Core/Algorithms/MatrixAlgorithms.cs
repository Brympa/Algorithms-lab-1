using AlgorithmLab.Core.Models;

namespace AlgorithmLab.Core.Algorithms;

public class MatrixMultiplyAlgorithm : IMatrixAlgorithm
{
    public string Id => "mat_mult";
    public string Name => "Умножение матриц: C = A × B";
    public AlgorithmCategory Category => AlgorithmCategory.Matrices;
    public ComplexityType Complexity => ComplexityType.ON3;
    public string ComplexityDisplay => "O(n³)";
    public string Description => "Классический трехцикловый алгоритм умножения матриц A(n×m) и B(m×n) с получением матрицы C(n×n).";
    public string PracticalApplication => "Компьютерная графика, машинное обучение (нейросети), решение систем линейных уравнений, 3D-моделирование.";
    public int DefaultNMin => 10;
    public int DefaultNMax => 1000; // Стартовое; жёсткого потолка в UI нет
    public int DefaultStep => 50;
    public int DefaultIterations => 3;

    public double[,] Execute(double[,] A, double[,] B)
    {
        int n = A.GetLength(0);
        int m = A.GetLength(1);
        int p = B.GetLength(1);

        double[,] C = new double[n, p];

        for (int i = 0; i < n; i++)
        {
            for (int k = 0; k < m; k++)
            {
                double a_ik = A[i, k];
                for (int j = 0; j < p; j++)
                {
                    C[i, j] += a_ik * B[k, j];
                }
            }
        }

        return C;
    }
}
