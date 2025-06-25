using TechDebtMaster.Cli.Tools.Shared;

namespace TechDebtMaster.Cli.Tools.Responses;

public class TechDebtStatisticsResponse
{
    public string RepositoryPath { get; set; } = string.Empty;
    public TechDebtFilters Filters { get; set; } = new();
    public TechDebtSummary Summary { get; set; } = new();
    public Dictionary<string, int> SeverityDistribution { get; init; } = [];
    public Dictionary<string, int> TagDistribution { get; init; } = [];
    public Dictionary<string, int> TopFilesByDebtCount { get; init; } = [];
    public Dictionary<string, int> CommonIssues { get; init; } = [];
}

public class RepositoryStatsResponse
{
    public string RepositoryPath { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public Dictionary<string, int> FileTypeDistribution { get; init; } = [];
    public int DirectoryCount { get; set; }
}

public class FileIssuesResponse
{
    public string RepositoryPath { get; set; } = string.Empty;
    public bool FileFound { get; set; }
    public IReadOnlyList<TechnicalDebtIssue> Issues { get; init; } = [];
    public FileIssuesSummary Summary { get; set; } = new();
    public TechDebtFilters Filters { get; set; } = new();
}

public class DebtItemResponse
{
    public string RepositoryPath { get; set; } = string.Empty;
    public string DebtId { get; set; } = string.Empty;
    public bool Found { get; set; }
    public string? Error { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DebtItemDetail? DebtItem { get; set; }
    public McpResourceInfo? Resource { get; set; }
}

public class RemoveDebtResponse
{
    public string RepositoryPath { get; set; } = string.Empty;
    public string DebtId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public bool ContentDeleted { get; set; }
    public bool MetadataDeleted { get; set; }
    public string? Message { get; set; }
}
