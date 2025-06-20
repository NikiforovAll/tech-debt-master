using System.Text.Json;

namespace TechDebtMaster.Cli.Services;

public interface IAnalysisService
{
    Task<AnalysisReport> AnalyzeChangedFilesAsync(
        Dictionary<string, string> changedFiles,
        string repositoryPath
    );
}

public class AnalysisService : IAnalysisService
{
    private readonly IIndexStorageService _storageService;
    private readonly IHashCalculator _hashCalculator;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AnalysisService(IIndexStorageService storageService, IHashCalculator hashCalculator)
    {
        _storageService = storageService;
        _hashCalculator = hashCalculator;
    }

    public async Task<AnalysisReport> AnalyzeChangedFilesAsync(
        Dictionary<string, string> changedFiles,
        string repositoryPath
    )
    {
        var previousReport = await LoadPreviousAnalysisAsync(repositoryPath);
        var report = new AnalysisReport { Timestamp = DateTime.UtcNow, FileHistories = [] };

        // Copy unchanged files from previous report
        if (previousReport != null)
        {
            foreach (var (path, history) in previousReport.FileHistories)
            {
                if (!changedFiles.ContainsKey(path))
                {
                    report.FileHistories[path] = history;
                }
            }
        }

        // Analyze changed files
        foreach (var (path, content) in changedFiles)
        {
            var currentAnalysis = new FileAnalysisEntry
            {
                Timestamp = DateTime.UtcNow,
                Preview = GetPreview(content),
                FileHash = _hashCalculator.CalculateHash(content),
            };

            FileAnalysisEntry? previousAnalysis = null;
            if (previousReport?.FileHistories.ContainsKey(path) == true)
            {
                previousAnalysis = previousReport.FileHistories[path].Current;
            }

            report.FileHistories[path] = new FileAnalysisHistory
            {
                FilePath = path,
                Current = currentAnalysis,
                Previous = previousAnalysis,
            };
        }

        await SaveAnalysisAsync(repositoryPath, report);
        return report;
    }

    private static string GetPreview(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var preview = content.Length > 100 ? content[..100] + "..." : content;

        // Replace newlines with spaces for better display
        return preview.Replace('\n', ' ').Replace('\r', ' ');
    }

    private async Task<AnalysisReport?> LoadPreviousAnalysisAsync(string repositoryPath)
    {
        var repoHash = GetRepositoryHash(repositoryPath);
        var analysisPath = GetAnalysisPath(repositoryPath, repoHash);

        if (!File.Exists(analysisPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(analysisPath);
            return JsonSerializer.Deserialize<AnalysisReport>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveAnalysisAsync(string repositoryPath, AnalysisReport report)
    {
        var repoHash = GetRepositoryHash(repositoryPath);
        var analysisPath = GetAnalysisPath(repositoryPath, repoHash);

        var indexDir = _storageService.GetIndexDirectory(repositoryPath);
        Directory.CreateDirectory(indexDir);

        var json = JsonSerializer.Serialize(report, _jsonOptions);
        await File.WriteAllTextAsync(analysisPath, json);
    }

    private string GetAnalysisPath(string repositoryPath, string repoHash)
    {
        var indexDir = _storageService.GetIndexDirectory(repositoryPath);
        return Path.Combine(indexDir, $"analysis_{repoHash}.json");
    }

    private string GetRepositoryHash(string repositoryPath)
    {
        var normalizedPath = Path.GetFullPath(repositoryPath).ToLowerInvariant();
        var hash = _hashCalculator.CalculateHash(normalizedPath);
        return hash.Substring(0, 8);
    }
}

public class AnalysisReport
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, FileAnalysisHistory> FileHistories { get; set; } = [];
}

public class FileAnalysisHistory
{
    public string FilePath { get; set; } = string.Empty;
    public FileAnalysisEntry Current { get; set; } = new();
    public FileAnalysisEntry? Previous { get; set; }
}

public class FileAnalysisEntry
{
    public DateTime Timestamp { get; set; }
    public string Preview { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
}
