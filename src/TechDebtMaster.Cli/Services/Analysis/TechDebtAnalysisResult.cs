namespace TechDebtMaster.Cli.Services.Analysis;

/// <summary>
/// Result of technical debt analysis stored in the main analysis file
/// </summary>
public class TechDebtAnalysisResult
{
    /// <summary>
    /// Collection of technical debt items found in the analysis
    /// </summary>
    public List<TechnicalDebtItem> Items { get; set; } = [];
}
