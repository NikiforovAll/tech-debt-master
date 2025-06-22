using System.Text.Json;

namespace TechDebtMaster.Cli.Services.Analysis;

public interface IAnalysisService
{
    Task<AnalysisReport> AnalyzeChangedFilesAsync(
        Dictionary<string, string> changedFiles,
        string repositoryPath
    );
    Task<AnalysisReport?> LoadAnalysisReportAsync(string repositoryPath);
    Task SaveAnalysisReportAsync(string repositoryPath, AnalysisReport report);
}

public class AnalysisService(
    IIndexStorageService storageService,
    IHashCalculator hashCalculator,
    IEnumerable<IAnalysisHandler> handlers
) : IAnalysisService
{
    private readonly IIndexStorageService _storageService = storageService;
    private readonly IHashCalculator _hashCalculator = hashCalculator;
    private readonly IEnumerable<IAnalysisHandler> _handlers = handlers;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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
            FileAnalysisEntry? previousAnalysis = null;
            if (previousReport?.FileHistories.ContainsKey(path) == true)
            {
                previousAnalysis = previousReport.FileHistories[path].Current;
            }

            var context = new FileAnalysisContext
            {
                FilePath = path,
                Content = content,
                FileHash = _hashCalculator.CalculateHash(content),
                Timestamp = DateTime.UtcNow,
                Previous = previousAnalysis,
            };

            // Process through all handlers
            foreach (var handler in _handlers)
            {
                await handler.ProcessAsync(context);
            }

            var currentAnalysis = new FileAnalysisEntry
            {
                Timestamp = context.Timestamp,
                AnalysisResults = context.Results,
                FileHash = context.FileHash,
            };

            report.FileHistories[path] = new FileAnalysisHistory
            {
                FilePath = path,
                Current = currentAnalysis,
                Previous = previousAnalysis,
            };
        }

        await SaveAnalysisReportAsync(repositoryPath, report);
        return report;
    }

    public async Task<AnalysisReport?> LoadAnalysisReportAsync(string repositoryPath)
    {
        return await LoadPreviousAnalysisAsync(repositoryPath);
    }

    private async Task<AnalysisReport?> LoadPreviousAnalysisAsync(string repositoryPath)
    {
        var repoHash = GetRepositoryHash(repositoryPath);
        var analysisPath = GetAnalysisPath(repositoryPath, repoHash);

        if (!File.Exists(analysisPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(analysisPath);
        return JsonSerializer.Deserialize<AnalysisReport>(json, _jsonOptions);
    }

    public async Task SaveAnalysisReportAsync(string repositoryPath, AnalysisReport report)
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
    public Dictionary<string, object> AnalysisResults { get; set; } = [];
    public string FileHash { get; set; } = string.Empty;
}
