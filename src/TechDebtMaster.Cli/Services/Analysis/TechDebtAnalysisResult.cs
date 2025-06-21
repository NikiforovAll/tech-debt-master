namespace TechDebtMaster.Cli.Services.Analysis;

/// <summary>
/// Result of technical debt analysis stored in the main analysis file
/// </summary>
public class TechDebtAnalysisResult
{
    /// <summary>
    /// Reference to the techdebt file containing the markdown analysis
    /// </summary>
    public TechDebtReference Reference { get; set; } = new();

    /// <summary>
    /// Overall severity level for this analysis
    /// </summary>
    public DebtSeverity Severity { get; set; }
}