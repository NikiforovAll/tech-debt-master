using System.Security.Cryptography;
using System.Text;

namespace TechDebtMaster.Cli.Services;

public interface IHashCalculator
{
    string CalculateHash(string content);
}

public class HashCalculator : IHashCalculator
{
    public string CalculateHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}