namespace TechDebtMaster.Cli.Services.Analysis;

/// <summary>
/// Represents a technical debt item found during analysis
/// </summary>
public class TechnicalDebtItem
{
    public string Summary { get; set; } = string.Empty;
    public DebtSeverity Severity { get; set; }
}