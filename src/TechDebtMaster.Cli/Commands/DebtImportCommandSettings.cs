using System.ComponentModel;
using Spectre.Console.Cli;

namespace TechDebtMaster.Cli.Commands;

public class DebtImportCommandSettings : CommandSettings
{
    [Description("Path to the modified HTML report file")]
    [CommandArgument(0, "<REPORT_PATH>")]
    public string ReportPath { get; init; } = string.Empty;

    [Description("Path to the repository (optional, uses default.repository or current directory)")]
    [CommandOption("-r|--repo")]
    public string? RepositoryPath { get; init; }

    [Description("Apply changes (default is dry-run mode)")]
    [CommandOption("--apply")]
    public bool Apply { get; init; }

    [Description("Show detailed information about items being removed")]
    [CommandOption("-v|--verbose")]
    public bool Verbose { get; init; }
}