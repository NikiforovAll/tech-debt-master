using TechDebtMaster.Cli.Services.Analysis;

namespace TechDebtMaster.Cli.Tools.Shared;

public class TechDebtFilters
{
    public string? IncludePattern { get; set; }
    public string? ExcludePattern { get; set; }
    public string? SeverityFilter { get; set; }
    public string? TagFilter { get; set; }
}

public class TechDebtSummary
{
    public int TotalFilesAnalyzed { get; set; }
    public int FilesDisplayed { get; set; }
    public int TotalDebtItems { get; set; }
    public int FilteredDebtItems { get; set; }
    public int FilesWithDebt { get; set; }
    public int FilteredFilesWithDebt { get; set; }
}

public class FileIssuesSummary
{
    public int TotalIssues { get; set; }
    public int FilteredIssues { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

public class TechnicalDebtIssue
{
    public string Id { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
}

public class DebtItemDetail
{
    public string Id { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public DateTime Timestamp { get; set; }
    public string ContentHash { get; set; } = string.Empty;
}

public class McpResourceInfo
{
    public Uri Uri { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class DebtItemWithFile
{
    public TechnicalDebtItem DebtItem { get; set; } = null!;
    public string FilePath { get; set; } = string.Empty;
}

public interface IRepositoryPathProvider
{
    string RepositoryPath { get; }
}

public class RepositoryPathProvider(string repositoryPath) : IRepositoryPathProvider
{
    public string RepositoryPath { get; } = repositoryPath;
}
