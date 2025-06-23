using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Services.Analysis.Handlers;

namespace TechDebtMaster.Cli.Commands;

public class DebtReportCommand(
    IAnalysisService analysisService,
    IConfigurationService configurationService,
    IHtmlReportGenerator htmlReportGenerator,
    ITechDebtStorageService techDebtStorageService
) : AsyncCommand<DebtReportCommandSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        DebtReportCommandSettings settings
    )
    {
        ArgumentNullException.ThrowIfNull(settings);

        var repositoryPath = settings.RepositoryPath;
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            var defaultRepo = await configurationService.GetAsync("default.repository");
            repositoryPath = !string.IsNullOrWhiteSpace(defaultRepo)
                ? defaultRepo
                : Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(repositoryPath))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Repository path '{repositoryPath}' does not exist."
            );
            return 1;
        }

        // Determine include/exclude patterns
        var includePattern = settings.IncludePattern;
        if (string.IsNullOrWhiteSpace(includePattern))
        {
            var defaultInclude = await configurationService.GetAsync("default.include");
            includePattern = !string.IsNullOrWhiteSpace(defaultInclude) ? defaultInclude : null;
        }

        var excludePattern = settings.ExcludePattern;
        if (string.IsNullOrWhiteSpace(excludePattern))
        {
            var defaultExclude = await configurationService.GetAsync("default.exclude");
            excludePattern = !string.IsNullOrWhiteSpace(defaultExclude) ? defaultExclude : null;
        }

        // Validate regex patterns
        try
        {
            if (!string.IsNullOrWhiteSpace(includePattern))
            {
                _ = new Regex(includePattern, RegexOptions.IgnoreCase);
            }
            if (!string.IsNullOrWhiteSpace(excludePattern))
            {
                _ = new Regex(excludePattern, RegexOptions.IgnoreCase);
            }
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid regex pattern - {ex.Message}");
            return 1;
        }

        // Load analysis report
        var analysisReport = await analysisService.LoadAnalysisReportAsync(repositoryPath);
        if (analysisReport == null)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]No analysis data found for repository:[/] {repositoryPath}"
            );
            AnsiConsole.MarkupLine(
                "[yellow]Run 'debt analyze <path>' first to generate analysis data.[/]"
            );
            return 0;
        }

        // Extract debt items
        var fileDebtMap = ExtractDebtItems(analysisReport);
        if (fileDebtMap.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No technical debt items found in analysis results.[/]");
            return 0;
        }

        // Apply filtering
        var filteredFileDebtMap = ApplyFiltering(fileDebtMap, includePattern, excludePattern);

        if (filteredFileDebtMap.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No files match the specified filters.[/]");
            return 0;
        }

        // Generate HTML report
        await AnsiConsole
            .Status()
            .StartAsync(
                "Generating HTML report...",
                async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));

                    // Load markdown content for all debt items
                    ctx.Status("Loading technical debt details...");
                    var fileDebtMapWithContent =
                        new Dictionary<string, List<TechnicalDebtItemWithContent>>();

                    foreach (var (filePath, debtItems) in filteredFileDebtMap)
                    {
                        var itemsWithContent = new List<TechnicalDebtItemWithContent>();

                        foreach (var item in debtItems)
                        {
                            var itemWithContent = new TechnicalDebtItemWithContent
                            {
                                Id = item.Id,
                                Summary = item.Summary,
                                Severity = item.Severity,
                                Tags = item.Tags,
                                Reference = item.Reference,
                                MarkdownContent = "",
                            };

                            try
                            {
                                var content = await techDebtStorageService.LoadTechDebtAsync(
                                    item.Reference
                                );
                                itemWithContent.MarkdownContent = content ?? "";
                            }
                            catch
                            {
                                // If content loading fails, continue with empty content
                                itemWithContent.MarkdownContent = "";
                            }

                            itemsWithContent.Add(itemWithContent);
                        }

                        fileDebtMapWithContent[filePath] = itemsWithContent;
                    }

                    ctx.Status("Generating HTML report...");
                    var repositoryName =
                        settings.RepositoryName
                        ?? Path.GetFileName(Path.GetFullPath(repositoryPath))
                        ?? "Repository";

                    var html = htmlReportGenerator.GenerateReport(
                        fileDebtMapWithContent,
                        repositoryName,
                        analysisReport.Timestamp
                    );

                    await File.WriteAllTextAsync(settings.OutputPath, html);
                }
            );

        var fullPath = Path.GetFullPath(settings.OutputPath);
        AnsiConsole.MarkupLine($"[green]Report generated successfully:[/] {fullPath}");

        // Open in browser if requested
        if (settings.OpenInBrowser)
        {
            try
            {
                OpenFileInBrowser(fullPath);
                AnsiConsole.MarkupLine("[green]Report opened in browser[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Could not open browser automatically: {ex.Message}[/]"
                );
            }
        }

        return 0;
    }

    private static Dictionary<string, List<TechnicalDebtItem>> ExtractDebtItems(
        AnalysisReport analysisReport
    )
    {
        var fileDebtMap = new Dictionary<string, List<TechnicalDebtItem>>();
        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        foreach (var (filePath, fileHistory) in analysisReport.FileHistories)
        {
            if (
                fileHistory.Current.AnalysisResults.TryGetValue(
                    TechDebtAnalysisHandler.ResultKey,
                    out var resultObj
                )
            )
            {
                TechDebtAnalysisResult? techDebtResult = null;
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(resultObj, jsonOptions);
                    techDebtResult =
                        System.Text.Json.JsonSerializer.Deserialize<TechDebtAnalysisResult>(
                            json,
                            jsonOptions
                        );
                }
                catch
                {
                    // Ignore deserialization errors
                }

                if (techDebtResult?.Items?.Count > 0)
                {
                    fileDebtMap[filePath] = techDebtResult.Items;
                }
            }
        }

        return fileDebtMap;
    }

    private static Dictionary<string, List<TechnicalDebtItem>> ApplyFiltering(
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap,
        string? includePattern,
        string? excludePattern
    )
    {
        var filteredMap = new Dictionary<string, List<TechnicalDebtItem>>();

        var includeRegex = !string.IsNullOrWhiteSpace(includePattern)
            ? new Regex(includePattern, RegexOptions.IgnoreCase)
            : null;
        var excludeRegex = !string.IsNullOrWhiteSpace(excludePattern)
            ? new Regex(excludePattern, RegexOptions.IgnoreCase)
            : null;

        foreach (var (filePath, debtItems) in fileDebtMap)
        {
            // Apply file pattern filtering
            if (includeRegex != null && !includeRegex.IsMatch(filePath))
            {
                continue;
            }

            if (excludeRegex != null && excludeRegex.IsMatch(filePath))
            {
                continue;
            }

            if (debtItems.Count > 0)
            {
                filteredMap[filePath] = debtItems;
            }
        }

        return filteredMap;
    }

    private static void OpenFileInBrowser(string filePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", filePath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", filePath);
        }
    }
}

public class TechnicalDebtItemWithContent
{
    public string Id { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DebtSeverity Severity { get; set; }
    public DebtTag[] Tags { get; set; } = [];
    public TechDebtReference Reference { get; set; } = new();
    public string MarkdownContent { get; set; } = string.Empty;
}
