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

public class RepositoryIndexService(
    IIndexStorageService storageService,
    IHashCalculator hashCalculator,
    IChangeDetector changeDetector,
    IRepomixParser repomixParser,
    IProcessRunner processRunner
) : IRepositoryIndexService
{
    private string _lastIndexedContent = string.Empty;

    public async Task<IndexResult> IndexRepositoryAsync(
        string repositoryPath,
        string? includePattern = null,
        string? excludePattern = null
    )
    {
        var repomixOutput = await processRunner.RunRepomixAsync(repositoryPath);
        var parsedData = repomixParser.ParseXmlOutput(repomixOutput);

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

        var previousIndex = await storageService.LoadLatestIndexAsync(repositoryPath);

        var currentIndex = new IndexData
        {
            Timestamp = DateTime.UtcNow,
            RepositoryPath = repositoryPath,
            Files = [],
        };

        foreach (var (path, fileData) in parsedData.Files)
        {
            currentIndex.Files[path] = new FileInfo
            {
                Hash = hashCalculator.CalculateHash(fileData.Content),
                Size = fileData.Content.Length,
                LastModified = DateTime.UtcNow,
            };
        }

        var changeSummary = changeDetector.DetectChanges(previousIndex, currentIndex);
        currentIndex.Summary = changeSummary;

        // Only save the index if there are changes
        if (changeSummary.HasChanges)
        {
            await storageService.SaveIndexAsync(repositoryPath, currentIndex);
        }

        return new IndexResult
        {
            FileSummary = parsedData.FileSummary,
            ChangeSummary = changeSummary,
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
}
