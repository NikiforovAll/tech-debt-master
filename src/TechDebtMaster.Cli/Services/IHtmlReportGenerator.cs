using TechDebtMaster.Cli.Commands;

namespace TechDebtMaster.Cli.Services;

public interface IHtmlReportGenerator
{
    string GenerateReport(
        Dictionary<string, List<TechnicalDebtItemWithContent>> fileDebtMap,
        string repositoryName,
        DateTime analysisDate
    );
}
