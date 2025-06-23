using TechDebtMaster.Cli.Services.Analysis;

namespace TechDebtMaster.Cli.Services;

public interface IReportStateExtractor
{
    Task<ReportState> ExtractStateAsync(string htmlReportPath);
}

public class ReportState
{
    public Dictionary<string, List<TechnicalDebtItem>> DebtData { get; init; } = [];
    public Dictionary<string, bool> HiddenItems { get; init; } = [];
    public Dictionary<string, bool> DoneItems { get; init; } = [];

    public bool IsItemActive(string filePath, string itemId)
    {
        var itemKey = $"{filePath}-{itemId}";
        return !HiddenItems.ContainsKey(itemKey) && !DoneItems.ContainsKey(itemKey);
    }
}