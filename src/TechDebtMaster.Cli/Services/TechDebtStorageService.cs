using TechDebtMaster.Cli.Services.Analysis;

namespace TechDebtMaster.Cli.Services;

/// <summary>
/// Service for storing and retrieving technical debt analysis results in separate files
/// </summary>
public class TechDebtStorageService(IHashCalculator hashCalculator) : ITechDebtStorageService
{
    private readonly IHashCalculator _hashCalculator = hashCalculator;

    public async Task<TechDebtReference> SaveTechDebtAsync(string markdownContent, string filePath)
    {
        // Normalize newlines to system newlines
        var normalizedContent = markdownContent
            .Replace("\r\n", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("\r", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("\n", Environment.NewLine, StringComparison.OrdinalIgnoreCase);

        var contentHash = _hashCalculator.CalculateMD5Hash(normalizedContent);
        var fileName = $"techdebt_{contentHash}.md";

        var techDebtDirectory = GetTechDebtDirectory();
        Directory.CreateDirectory(techDebtDirectory);

        var fullPath = Path.Combine(techDebtDirectory, fileName);

        // Store the markdown content directly (not as JSON)
        await File.WriteAllTextAsync(fullPath, normalizedContent);

        return new TechDebtReference
        {
            FileName = fileName,
            Timestamp = DateTime.UtcNow,
            ContentHash = contentHash,
        };
    }

    public async Task<string> LoadTechDebtAsync(TechDebtReference reference)
    {
        try
        {
            var fullPath = Path.Combine(GetTechDebtDirectory(), reference.FileName);

            if (!File.Exists(fullPath))
            {
                return string.Empty;
            }

            // Read the markdown content directly (not as JSON)
            var markdownContent = await File.ReadAllTextAsync(fullPath);

            return markdownContent;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<bool> ExistsAsync(TechDebtReference reference)
    {
        var fullPath = Path.Combine(GetTechDebtDirectory(), reference.FileName);
        return await Task.FromResult(File.Exists(fullPath));
    }

    public async Task<bool> DeleteAsync(TechDebtReference reference)
    {
        try
        {
            var fullPath = Path.Combine(GetTechDebtDirectory(), reference.FileName);

            if (!File.Exists(fullPath))
            {
                return false;
            }

            File.Delete(fullPath);
            return await Task.FromResult(true);
        }
        catch
        {
            return false;
        }
    }

    private static string GetTechDebtDirectory()
    {
        var baseDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".tdm");
        return Path.Combine(baseDirectory, "techdebt");
    }
}
