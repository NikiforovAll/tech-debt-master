using System.Security.Cryptography;
using System.Text;

namespace TechDebtMaster.Cli.Services;

public interface IHashCalculator
{
    string CalculateHash(string content);
    string CalculateMD5Hash(string content);
}

public class HashCalculator : IHashCalculator
{
    public string CalculateHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    public string CalculateMD5Hash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
        var hashBytes = MD5.HashData(bytes);
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
