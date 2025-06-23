namespace TechDebtMaster.Cli.Services.Analysis;

/// <summary>
/// Reference to a technical debt analysis result stored in a separate file
/// </summary>
public class TechDebtReference
{
    /// <summary>
    /// MD5-based filename for the techdebt analysis file
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the analysis was performed
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// MD5 hash of the analysis content for integrity verification
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;
}
