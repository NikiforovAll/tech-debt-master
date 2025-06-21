namespace TechDebtMaster.Cli.Services.Analysis;

/// <summary>
/// Represents a technical debt item found during analysis
/// </summary>
public class TechnicalDebtItem
{
    public string Id { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DebtSeverity Severity { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
