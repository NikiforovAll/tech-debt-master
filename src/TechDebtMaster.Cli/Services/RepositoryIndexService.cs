using System.Text.RegularExpressions;

namespace TechDebtMaster.Cli.Services;

public interface IRepositoryIndexService
{
    Task<IndexResult> IndexRepositoryAsync(
        string repositoryPath,
        string? includePattern = null,
        string? excludePattern = null
    );
    string GetLastIndexedContent();
}

public class IndexResult
{
    public string FileSummary { get; set; } = string.Empty;
    public IndexSummary ChangeSummary { get; set; } = new();
    public AnalysisReport? AnalysisReport { get; set; }
    public FilteringStats? FilteringStats { get; set; }
    public bool HasChanges =>
        ChangeSummary.ChangedFiles.Any()
        || ChangeSummary.NewFiles.Any()
        || ChangeSummary.DeletedFiles.Any();
}

public class FilteringStats
{
    public int TotalFiles { get; set; }
    public int FilteredFiles { get; set; }
    public int ExcludedFiles => TotalFiles - FilteredFiles;
    public string? IncludePattern { get; set; }
    public string? ExcludePattern { get; set; }
    public bool WasFiltered =>
        !string.IsNullOrWhiteSpace(IncludePattern) || !string.IsNullOrWhiteSpace(ExcludePattern);
}

public class RepositoryIndexService : IRepositoryIndexService
{
    private readonly IIndexStorageService _storageService;
    private readonly IHashCalculator _hashCalculator;
    private readonly IChangeDetector _changeDetector;
    private readonly IRepomixParser _repomixParser;
    private readonly IAnalysisService _analysisService;
    private string _lastIndexedContent = string.Empty;

    public RepositoryIndexService(
        IIndexStorageService storageService,
        IHashCalculator hashCalculator,
        IChangeDetector changeDetector,
        IRepomixParser repomixParser,
        IAnalysisService analysisService
    )
    {
        _storageService = storageService;
        _hashCalculator = hashCalculator;
        _changeDetector = changeDetector;
        _repomixParser = repomixParser;
        _analysisService = analysisService;
    }

    public async Task<IndexResult> IndexRepositoryAsync(
        string repositoryPath,
        string? includePattern = null,
        string? excludePattern = null
    )
    {
        var repomixOutput = await RunRepomixAsync(repositoryPath);
        var parsedData = _repomixParser.ParseXmlOutput(repomixOutput);

        // Apply filtering if patterns are provided
        var originalFileCount = parsedData.Files.Count;
        FilteringStats? filteringStats = null;

        if (
            !string.IsNullOrWhiteSpace(includePattern) || !string.IsNullOrWhiteSpace(excludePattern)
        )
        {
            parsedData = ApplyFileFiltering(parsedData, includePattern, excludePattern);

            filteringStats = new FilteringStats
            {
                TotalFiles = originalFileCount,
                FilteredFiles = parsedData.Files.Count,
                IncludePattern = includePattern,
                ExcludePattern = excludePattern,
            };
        }

        _lastIndexedContent = parsedData.FileSummary;

        var previousIndex = await _storageService.LoadLatestIndexAsync(repositoryPath);

        var currentIndex = new IndexData
        {
            Timestamp = DateTime.UtcNow,
            RepositoryPath = repositoryPath,
            Files = new Dictionary<string, FileInfo>(),
        };

        foreach (var (path, fileData) in parsedData.Files)
        {
            currentIndex.Files[path] = new FileInfo
            {
                Hash = _hashCalculator.CalculateHash(fileData.Content),
                Size = fileData.Content.Length,
                LastModified = DateTime.UtcNow,
            };
        }

        var changeSummary = _changeDetector.DetectChanges(previousIndex, currentIndex);
        currentIndex.Summary = changeSummary;

        // Only save the index if there are changes
        if (changeSummary.HasChanges)
        {
            await _storageService.SaveIndexAsync(repositoryPath, currentIndex);
        }

        // Prepare files for analysis (changed and new files)
        var filesToAnalyze = new Dictionary<string, string>();
        foreach (
            var path in changeSummary
                .ChangedFiles.Concat(changeSummary.NewFiles)
                .Where(path => parsedData.Files.ContainsKey(path))
        )
        {
            filesToAnalyze[path] = parsedData.Files[path].Content;
        }

        // Run analysis on changed files
        AnalysisReport? analysisReport = null;
        if (filesToAnalyze.Any())
        {
            analysisReport = await _analysisService.AnalyzeChangedFilesAsync(
                filesToAnalyze,
                repositoryPath
            );
        }

        return new IndexResult
        {
            FileSummary = parsedData.FileSummary,
            ChangeSummary = changeSummary,
            AnalysisReport = analysisReport,
            FilteringStats = filteringStats,
        };
    }

    public string GetLastIndexedContent()
    {
        return _lastIndexedContent;
    }

    private static RepomixData ApplyFileFiltering(
        RepomixData data,
        string? includePattern,
        string? excludePattern
    )
    {
        var filteredData = new RepomixData { FileSummary = data.FileSummary, Files = [] };

        Regex? includeRegex = null;
        Regex? excludeRegex = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(includePattern))
            {
                includeRegex = new Regex(includePattern, RegexOptions.IgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(excludePattern))
            {
                excludeRegex = new Regex(excludePattern, RegexOptions.IgnoreCase);
            }
        }
        catch (ArgumentException ex)
        {
            var problemPattern = !string.IsNullOrWhiteSpace(includePattern)
                ? $"Include pattern '{includePattern}'"
                : $"Exclude pattern '{excludePattern}'";
            throw new InvalidOperationException(
                $"Invalid regex pattern - {problemPattern}: {ex.Message}",
                ex
            );
        }

        foreach (var (filePath, fileData) in data.Files)
        {
            var shouldInclude = true;

            // Apply include filter
            if (includeRegex != null)
            {
                shouldInclude = includeRegex.IsMatch(filePath);
            }

            // Apply exclude filter
            if (shouldInclude && excludeRegex != null)
            {
                shouldInclude = !excludeRegex.IsMatch(filePath);
            }

            if (shouldInclude)
            {
                filteredData.Files[filePath] = fileData;
            }
        }

        return filteredData;
    }

    private static async Task<string> RunRepomixAsync(string repositoryPath)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "repomix.cmd";
            process.StartInfo.Arguments = $"--stdout --style xml \"{repositoryPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = errorBuilder.ToString();
                throw new InvalidOperationException($"Repomix failed with error: {error}");
            }

            return outputBuilder.ToString();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to run repomix. Make sure it's installed and in PATH: {ex.Message}",
                ex
            );
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Unexpected error running repomix: {ex.Message}",
                ex
            );
        }
    }
}
