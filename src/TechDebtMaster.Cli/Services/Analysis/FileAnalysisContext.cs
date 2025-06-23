namespace TechDebtMaster.Cli.Services.Analysis;

/// <summary>
/// Context object passed between analysis handlers containing file information and analysis results
/// </summary>
public class FileAnalysisContext
{
    /// <summary>
    /// Path of the file being analyzed
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Content of the file being analyzed
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Hash of the file content
    /// </summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the analysis was performed
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Dictionary of analysis results from different handlers
    /// Key: Handler name or result type
    /// Value: Analysis result (can be string, object, etc.)
    /// </summary>
    public Dictionary<string, object> Results { get; set; } = [];

    /// <summary>
    /// Previous analysis entry for comparison (if available)
    /// </summary>
    public FileAnalysisEntry? Previous { get; set; }
}
