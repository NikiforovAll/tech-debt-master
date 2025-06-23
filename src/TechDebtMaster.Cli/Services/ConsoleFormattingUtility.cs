using Spectre.Console;
using TechDebtMaster.Cli.Services.Analysis;

namespace TechDebtMaster.Cli.Services;

public static class ConsoleFormattingUtility
{
    public static string GetSeverityColor(DebtSeverity severity)
    {
        return severity switch
        {
            DebtSeverity.Critical => "red",
            DebtSeverity.High => "orange3",
            DebtSeverity.Medium => "yellow",
            DebtSeverity.Low => "green",
            _ => "gray",
        };
    }

    public static Color GetSeverityColorEnum(DebtSeverity severity)
    {
        return severity switch
        {
            DebtSeverity.Critical => Color.Red,
            DebtSeverity.High => Color.Orange3,
            DebtSeverity.Medium => Color.Yellow,
            DebtSeverity.Low => Color.Green,
            _ => Color.Grey,
        };
    }

    public static string GetSeverityLabel(DebtSeverity severity)
    {
        return severity.ToString().ToUpper();
    }

    public static string FormatSeverityWithColor(DebtSeverity severity)
    {
        var color = GetSeverityColor(severity);
        var label = GetSeverityLabel(severity);
        return $"[{color}]{label}[/]";
    }
}
