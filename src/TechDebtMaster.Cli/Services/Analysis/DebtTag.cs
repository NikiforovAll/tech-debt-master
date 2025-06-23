using System.Text.Json.Serialization;

namespace TechDebtMaster.Cli.Services.Analysis;

/// <summary>
/// Represents the different types of technical debt tags
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DebtTag
{
    CodeSmell,
    Naming,
    MagicNumber,
    Complexity,
    ErrorHandling,
    OutdatedPattern,
    Todo,
    Performance,
    Security,
    General
}
