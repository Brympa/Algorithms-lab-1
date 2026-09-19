using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace AlgorithmLab.Core.Dataset;

public class DatasetConfig
{
    public string Version { get; set; } = "1.0";
    public int Seed { get; set; } = 42;
    public string Distribution { get; set; } = "RandomUniform"; // RandomUniform, SortedAscending, SortedDescending, AlmostSorted
    public int MaxPrecomputedSize { get; set; } = 20000;
    public double PolynomialX { get; set; } = 1.5;
    public double PowerBase { get; set; } = 1.05;
    public Dictionary<int, double[]>? CustomVectors { get; set; }

    public string ComputeHash()
    {
        var raw = $"{Version}:{Seed}:{Distribution}:{MaxPrecomputedSize}:{PolynomialX}:{PowerBase}:{CustomVectors?.Count ?? 0}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16];
    }
}
