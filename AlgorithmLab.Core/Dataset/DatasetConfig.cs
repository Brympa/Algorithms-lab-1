using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

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
        // Включаем содержимое CustomVectors, чтобы разные векторы при одинаковом Count
        // не давали коллизию ключа кэша.
        var vectorsPart = string.Empty;
        if (CustomVectors != null)
        {
            var sb = new StringBuilder();
            foreach (var kv in CustomVectors.OrderBy(k => k.Key))
            {
                sb.Append(kv.Key).Append(':');
                foreach (var v in kv.Value)
                {
                    sb.Append(v.ToString("R")).Append(',');
                }
                sb.Append(';');
            }
            vectorsPart = sb.ToString();
        }

        var raw = $"{Version}:{Seed}:{Distribution}:{MaxPrecomputedSize}:{PolynomialX}:{PowerBase}:{vectorsPart}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16];
    }
}
