using System.Text.Json.Serialization;

namespace TechDebtMaster.Cli.Services.Analysis;

/// <summary>
/// Represents the severity level of technical debt
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DebtSeverity
{
    Low,
    Medium,
    High,
    Critical
}