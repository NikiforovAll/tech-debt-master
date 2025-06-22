using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Services.Analysis.Handlers;
using TextCopy;

namespace TechDebtMaster.Cli.Commands;

[Description("View detailed content of specific technical debt items")]
public class AnalyzeViewCommand(
    IAnalysisService analysisService,
    ITechDebtStorageService techDebtStorage,
    IConfigurationService configurationService
) : AsyncCommand<AnalyzeViewCommand.Settings>
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var repositoryPath = settings.RepositoryPath;
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            // Try to get default from configuration
            var defaultRepo = await configurationService.GetAsync("default.repository");
            repositoryPath = !string.IsNullOrWhiteSpace(defaultRepo)
                ? defaultRepo
                : Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(repositoryPath))
        {
            if (!IsNonInteractiveMode(settings))
            {
                AnsiConsole.MarkupLine(
                    $"[red]Error:[/] Repository path '{repositoryPath}' does not exist."
                );
            }
            return 1;
        }

        // Determine include/exclude patterns (command line takes priority over defaults)
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

        // Validate regex patterns before proceeding
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
            if (!IsNonInteractiveMode(settings))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid regex pattern - {ex.Message}");
            }
            return 1;
        }

        // Load analysis report
        var analysisReport = await analysisService.LoadAnalysisReportAsync(repositoryPath);
        if (analysisReport == null)
        {
            if (!IsNonInteractiveMode(settings))
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]No analysis data found for repository:[/] {repositoryPath}"
                );
                AnsiConsole.MarkupLine(
                    "[yellow]Run 'analyze debt <path>' first to generate analysis data.[/]"
                );
            }
            return 0;
        }

        // Extract debt items from analysis report
        var debtItems = ExtractDebtItems(analysisReport);

        if (debtItems.Count == 0)
        {
            if (!IsNonInteractiveMode(settings))
            {
                AnsiConsole.MarkupLine(
                    "[yellow]No technical debt items found in analysis results.[/]"
                );
            }
            return 0;
        }

        // Check if specific debt ID is requested (takes precedence over all filtering)
        if (!string.IsNullOrWhiteSpace(settings.DebtId))
        {
            if (!TryParseDebtId(settings.DebtId, out var targetFilePath, out var targetItemId))
            {
                if (!IsNonInteractiveMode(settings))
                {
                    AnsiConsole.MarkupLine(
                        "[red]Error:[/] Invalid debt-id format. Expected '<pathToFile>:debtId'"
                    );
                    AnsiConsole.MarkupLine(
                        "Example: [cyan]src/Controllers/UserController.cs:TD001[/]"
                    );
                }
                return 1;
            }

            var specificItem = FindSpecificDebtItem(debtItems, targetFilePath, targetItemId);
            if (specificItem == null)
            {
                if (!IsNonInteractiveMode(settings))
                {
                    AnsiConsole.MarkupLine(
                        $"[red]Error:[/] Debt item '[yellow]{targetItemId}[/]' not found in file '[yellow]{targetFilePath}[/]'"
                    );

                    // Suggest similar items
                    var similarItems = debtItems
                        .Where(item =>
                            string.Equals(
                                item.DebtItem.Id,
                                targetItemId,
                                StringComparison.OrdinalIgnoreCase
                            )
                            || item.FilePath.Contains(
                                Path.GetFileName(targetFilePath),
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        .Take(3)
                        .ToList();

                    if (similarItems.Count > 0)
                    {
                        AnsiConsole.MarkupLine("[yellow]Similar items found:[/]");
                        foreach (var similar in similarItems)
                        {
                            AnsiConsole.MarkupLine(
                                $"  [cyan]{similar.FilePath}:{similar.DebtItem.Id}[/] - {similar.DebtItem.Summary}"
                            );
                        }
                    }
                }
                return 1;
            }

            // Check if output format is specified for single item
            if (settings.PlainOutput || settings.JsonOutput || settings.XmlOutput)
            {
                var singleItemList = new List<DebtItemWithFile> { specificItem };
                var itemsWithContent = await LoadContentForAllItems(singleItemList);

                if (settings.PlainOutput)
                {
                    OutputPlainFormat(itemsWithContent);
                }
                else if (settings.JsonOutput)
                {
                    OutputJsonFormat(itemsWithContent);
                }
                else if (settings.XmlOutput)
                {
                    OutputXmlFormat(itemsWithContent);
                }
            }
            else
            {
                // Display in interactive format for single item
                if (settings.InteractiveMode)
                {
                    await DisplayDebtItemDetailInteractive(
                        specificItem.DebtItem,
                        specificItem.FilePath
                    );
                }
                else
                {
                    await DisplayDebtItemDetail(specificItem.DebtItem, specificItem.FilePath);
                }
            }

            return 0;
        }

        // Apply filtering if patterns are provided (only when debt-id not specified)
        var filteredDebtItems = ApplyFiltering(debtItems, settings, includePattern, excludePattern);

        if (filteredDebtItems.Count == 0)
        {
            if (!IsNonInteractiveMode(settings))
            {
                AnsiConsole.MarkupLine("[yellow]No debt items match the specified filters.[/]");
            }
            return 0;
        }

        // Display filtering information
        if (!IsNonInteractiveMode(settings))
        {
            DisplayFilteringInfo(
                debtItems,
                filteredDebtItems,
                settings,
                includePattern,
                excludePattern
            );
        }

        // Check if output format is specified
        if (settings.PlainOutput || settings.JsonOutput || settings.XmlOutput)
        {
            // Validate that only one output format is specified
            var formatCount =
                (settings.PlainOutput ? 1 : 0)
                + (settings.JsonOutput ? 1 : 0)
                + (settings.XmlOutput ? 1 : 0);
            if (formatCount > 1)
            {
                if (!IsNonInteractiveMode(settings))
                {
                    AnsiConsole.MarkupLine(
                        "[red]Error:[/] Only one output format can be specified at a time."
                    );
                }
                return 1;
            }

            // Load content for all filtered items and output in specified format
            var itemsWithContent = await LoadContentForAllItems(filteredDebtItems);

            if (settings.PlainOutput)
            {
                OutputPlainFormat(itemsWithContent);
            }
            else if (settings.JsonOutput)
            {
                OutputJsonFormat(itemsWithContent);
            }
            else if (settings.XmlOutput)
            {
                OutputXmlFormat(itemsWithContent);
            }

            return 0;
        }

        // Interactive mode - Create selection options ordered by priority (severity)
        var selectionOptions = filteredDebtItems
            .OrderByDescending(item => item.DebtItem.Severity)
            .ThenBy(item => item.FilePath)
            .Select(item => new DebtItemOption
            {
                DisplayText = FormatDebtItemForSelection(item),
                DebtItem = item.DebtItem,
                FilePath = item.FilePath,
            })
            .ToList();

        // Add exit option if in interactive mode
        if (settings.InteractiveMode)
        {
            selectionOptions.Add(
                new DebtItemOption
                {
                    DisplayText = "[red]Exit[/]",
                    DebtItem = null!,
                    FilePath = "[EXIT]",
                }
            );
        }

        // Interactive mode loop
        while (true)
        {
            if (settings.InteractiveMode)
            {
                AnsiConsole.Clear();
            }

            // Present selection prompt
            var title = settings.InteractiveMode
                ? "Select a [green]technical debt item[/] to view (interactive mode):"
                : "Select a [green]technical debt item[/] to view:";

            var selectedOption = AnsiConsole.Prompt(
                new SelectionPrompt<DebtItemOption>()
                    .Title(title)
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up and down to reveal more items)[/]")
                    .UseConverter(option => option.DisplayText)
                    .AddChoices(selectionOptions)
            );

            // Check if exit was selected
            if (settings.InteractiveMode && selectedOption.FilePath == "[EXIT]")
            {
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine("[green]Exiting interactive mode.[/]");
                break;
            }

            // Load and display detailed content
            if (settings.InteractiveMode)
            {
                await DisplayDebtItemDetailInteractive(
                    selectedOption.DebtItem,
                    selectedOption.FilePath
                );
            }
            else
            {
                await DisplayDebtItemDetail(selectedOption.DebtItem, selectedOption.FilePath);
                break; // Exit after single display in non-interactive mode
            }
        }

        return 0;
    }

    private static List<DebtItemWithFile> ExtractDebtItems(AnalysisReport analysisReport)
    {
        var debtItems = new List<DebtItemWithFile>();
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
                    foreach (var item in techDebtResult.Items)
                    {
                        debtItems.Add(
                            new DebtItemWithFile { DebtItem = item, FilePath = filePath }
                        );
                    }
                }
            }
        }

        return debtItems;
    }

    private static List<DebtItemWithFile> ApplyFiltering(
        List<DebtItemWithFile> debtItems,
        Settings settings,
        string? includePattern,
        string? excludePattern
    )
    {
        var includeRegex = !string.IsNullOrWhiteSpace(includePattern)
            ? new Regex(includePattern, RegexOptions.IgnoreCase)
            : null;
        var excludeRegex = !string.IsNullOrWhiteSpace(excludePattern)
            ? new Regex(excludePattern, RegexOptions.IgnoreCase)
            : null;

        return
        [
            .. debtItems.Where(item =>
            {
                // Apply file pattern filtering
                if (includeRegex != null && !includeRegex.IsMatch(item.FilePath))
                {
                    return false;
                }

                if (excludeRegex != null && excludeRegex.IsMatch(item.FilePath))
                {
                    return false;
                }

                // Apply severity filtering
                if (
                    settings.SeverityFilter.HasValue
                    && item.DebtItem.Severity != settings.SeverityFilter.Value
                )
                {
                    return false;
                }

                // Apply tag filtering
                if (
                    settings.TagFilter.HasValue
                    && !item.DebtItem.Tags.Contains(settings.TagFilter.Value)
                )
                {
                    return false;
                }

                return true;
            }),
        ];
    }

    private static void DisplayFilteringInfo(
        List<DebtItemWithFile> original,
        List<DebtItemWithFile> filtered,
        Settings settings,
        string? includePattern,
        string? excludePattern
    )
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[blue]Total debt items found:[/] {original.Count}");

        if (filtered.Count != original.Count)
        {
            AnsiConsole.MarkupLine($"[blue]Items after filtering:[/] {filtered.Count}");
        }

        if (!string.IsNullOrWhiteSpace(includePattern))
        {
            var source = settings.IncludePattern == includePattern ? "" : " (from default.include)";
            AnsiConsole.MarkupLine(
                $"[yellow]Include pattern{source}:[/] [cyan]{includePattern}[/]"
            );
        }
        if (!string.IsNullOrWhiteSpace(excludePattern))
        {
            var source = settings.ExcludePattern == excludePattern ? "" : " (from default.exclude)";
            AnsiConsole.MarkupLine(
                $"[yellow]Exclude pattern{source}:[/] [cyan]{excludePattern}[/]"
            );
        }
        if (settings.SeverityFilter.HasValue)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Severity filter:[/] [cyan]{settings.SeverityFilter.Value}[/]"
            );
        }
        if (settings.TagFilter.HasValue)
        {
            AnsiConsole.MarkupLine($"[yellow]Tag filter:[/] [cyan]{settings.TagFilter.Value}[/]");
        }

        AnsiConsole.WriteLine();
    }

    private static string FormatDebtItemForSelection(DebtItemWithFile item)
    {
        var severityColor = GetSeverityColor(item.DebtItem.Severity);
        var fileName = Path.GetFileName(item.FilePath);
        var tagsText =
            item.DebtItem.Tags.Length > 0 ? $" ({string.Join(", ", item.DebtItem.Tags)})" : "";

        return $"[{severityColor}]{item.DebtItem.Severity}[/] [{severityColor}]●[/] [bold]{item.DebtItem.Id}[/]: {item.DebtItem.Summary} [dim]in {fileName}[/]{tagsText}";
    }

    private async Task DisplayDebtItemDetail(TechnicalDebtItem debtItem, string filePath)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Rule($"[bold]{filePath.EscapeMarkup()}:{debtItem.Id.EscapeMarkup()}[/]")
        );
        AnsiConsole.WriteLine();

        // Display item summary information
        var severityColor = GetSeverityColor(debtItem.Severity);

        var table = new Table()
            .AddColumn("[bold]Property[/]")
            .AddColumn("[bold]Value[/]")
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddRow("[cyan]ID[/]", $"[bold]{debtItem.Id}[/]");
        table.AddRow("[cyan]File[/]", $"[blue]{filePath}[/]");
        table.AddRow("[cyan]Summary[/]", debtItem.Summary);
        table.AddRow("[cyan]Severity[/]", $"[{severityColor}]{debtItem.Severity}[/]");

        if (debtItem.Tags.Length > 0)
        {
            table.AddRow(
                "[cyan]Tags[/]",
                string.Join(", ", debtItem.Tags.Select(t => $"[yellow]{t}[/]"))
            );
        }

        table.AddRow(
            "[cyan]Analysis Date[/]",
            $"[dim]{debtItem.Reference.Timestamp:yyyy-MM-dd HH:mm:ss} UTC[/]"
        );

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Load and display detailed content
        try
        {
            AnsiConsole
                .Status()
                .Start(
                    "Loading detailed analysis...",
                    ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        ctx.SpinnerStyle(Style.Parse("green"));
                    }
                );

            var detailedContent = await techDebtStorage.LoadTechDebtAsync(debtItem.Reference);

            if (string.IsNullOrWhiteSpace(detailedContent))
            {
                AnsiConsole.MarkupLine(
                    "[yellow]No detailed content available for this debt item.[/]"
                );
                AnsiConsole.MarkupLine("[dim]The content file may have been deleted or moved.[/]");
                return;
            }

            AnsiConsole.Write(new Rule("[bold]Detailed Analysis[/]"));
            AnsiConsole.WriteLine();

            // Display the markdown content with basic formatting
            DisplayMarkdownContent(detailedContent);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error loading detailed content:[/] {ex.Message}");
        }
    }

    private async Task DisplayDebtItemDetailInteractive(TechnicalDebtItem debtItem, string filePath)
    {
        AnsiConsole.Clear();

        // Display the debt item detail
        await DisplayDebtItemDetail(debtItem, filePath);

        // Display navigation instructions
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule());
        AnsiConsole.MarkupLine(
            "[dim]Press [/][yellow]'q'[/][dim] to go back to selection | [/][yellow]'c'[/][dim] to copy ID to clipboard[/]"
        );

        // Wait for user input
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Q || key.KeyChar == 'q' || key.KeyChar == 'Q')
            {
                break;
            }
            else if (key.Key == ConsoleKey.C || key.KeyChar == 'c' || key.KeyChar == 'C')
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    // Copy the full debt ID (filepath:id) to clipboard
                    var fullDebtId = $"{filePath}:{debtItem.Id}";
                    await ClipboardService.SetTextAsync(fullDebtId);

                    // Show confirmation message by moving cursor up and rewriting the line
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.Write("\r" + new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                    AnsiConsole.MarkupLine(
                        $"[green]✓ Copied to clipboard:[/] [cyan]{fullDebtId.EscapeMarkup()}[/]"
                    );

                    // Wait a moment then restore the instruction line
                    await Task.Delay(2000);
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.Write("\r" + new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                    AnsiConsole.MarkupLine(
                        "[dim]Press [/][yellow]'q'[/][dim] to go back to selection | [/][yellow]'c'[/][dim] to copy ID to clipboard[/]"
                    );
                }
                catch (Exception ex)
                {
                    // Show error message
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.Write("\r" + new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                    AnsiConsole.MarkupLine(
                        $"[red]Failed to copy to clipboard:[/] {ex.Message.EscapeMarkup()}"
                    );

                    // Wait a moment then restore the instruction line
                    await Task.Delay(2000);
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.Write("\r" + new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                    AnsiConsole.MarkupLine(
                        "[dim]Press [/][yellow]'q'[/][dim] to go back to selection | [/][yellow]'c'[/][dim] to copy ID to clipboard[/]"
                    );
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }
        }
    }

    private static void DisplayMarkdownContent(string markdownContent)
    {
        // Split content into lines for processing
        var lines = markdownContent.Split('\n', StringSplitOptions.None);

        // Define color hierarchy for headers (H1-H6)
        var headerColors = new[]
        {
            "bold blue", // H1 - Primary sections (Context, Scenario, etc.)
            "bold green", // H2 - Major subsections
            "bold yellow", // H3 - Minor subsections
            "bold magenta", // H4 - Detail sections
            "bold cyan", // H5 - Sub-details
            "bold white", // H6 - Fine details
        };

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd().EscapeMarkup();

            // Handle headers - count the number of hash symbols
            if (trimmedLine.StartsWith('#'))
            {
                var hashCount = 0;
                for (int i = 0; i < trimmedLine.Length && trimmedLine[i] == '#'; i++)
                {
                    hashCount++;
                }

                // Ensure we have a valid header (1-6 levels) and there's text after the hashes
                if (hashCount >= 1 && hashCount <= 6 && hashCount < trimmedLine.Length)
                {
                    var headerText = trimmedLine[hashCount..].Trim();
                    if (!string.IsNullOrEmpty(headerText))
                    {
                        var colorIndex = hashCount - 1; // Convert to 0-based index
                        var color = headerColors[colorIndex];
                        AnsiConsole.MarkupLine($"[{color}]{headerText}[/]");
                    }
                }
            }
            // Handle code blocks (simple detection)
            else if (trimmedLine.StartsWith("```", StringComparison.Ordinal))
            {
                AnsiConsole.MarkupLine($"[dim]{trimmedLine}[/]");
            }
            // Handle bullet points
            else if (
                trimmedLine.StartsWith("- ", StringComparison.Ordinal)
                || trimmedLine.StartsWith("* ", StringComparison.Ordinal)
            )
            {
                var bulletText = trimmedLine[2..];
                AnsiConsole.MarkupLine($"  [cyan]•[/] {bulletText}");
            }
            // Handle numbered lists
            else if (Regex.IsMatch(trimmedLine, @"^\d+\.\s"))
            {
                AnsiConsole.MarkupLine($"  {trimmedLine}");
            }
            // Handle empty lines
            else if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                AnsiConsole.WriteLine();
            }
            // Regular text
            else
            {
                AnsiConsole.MarkupLine(trimmedLine);
            }
        }
    }

    private static bool TryParseDebtId(string debtId, out string filePath, out string itemId)
    {
        filePath = string.Empty;
        itemId = string.Empty;

        if (string.IsNullOrWhiteSpace(debtId))
        {
            return false;
        }

        var lastColonIndex = debtId.LastIndexOf(':');
        if (lastColonIndex <= 0 || lastColonIndex == debtId.Length - 1)
        {
            return false;
        }

        filePath = debtId.Substring(0, lastColonIndex);
        itemId = debtId.Substring(lastColonIndex + 1);

        return !string.IsNullOrWhiteSpace(filePath) && !string.IsNullOrWhiteSpace(itemId);
    }

    private static DebtItemWithFile? FindSpecificDebtItem(
        List<DebtItemWithFile> debtItems,
        string targetFilePath,
        string targetItemId
    )
    {
        // First try exact file path match
        var exactMatch = debtItems.FirstOrDefault(item =>
            string.Equals(item.FilePath, targetFilePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.DebtItem.Id, targetItemId, StringComparison.OrdinalIgnoreCase)
        );

        if (exactMatch != null)
        {
            return exactMatch;
        }

        // Try relative path match (match end of file path)
        var relativeMatch = debtItems.FirstOrDefault(item =>
            item.FilePath.EndsWith(targetFilePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.DebtItem.Id, targetItemId, StringComparison.OrdinalIgnoreCase)
        );

        if (relativeMatch != null)
        {
            return relativeMatch;
        }

        // Try filename only match
        var targetFileName = Path.GetFileName(targetFilePath);
        var filenameMatch = debtItems.FirstOrDefault(item =>
            string.Equals(
                Path.GetFileName(item.FilePath),
                targetFileName,
                StringComparison.OrdinalIgnoreCase
            ) && string.Equals(item.DebtItem.Id, targetItemId, StringComparison.OrdinalIgnoreCase)
        );

        return filenameMatch;
    }

    private static string GetSeverityColor(DebtSeverity severity)
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

    private static void OutputPlainFormat(List<DebtItemWithContent> items)
    {
        foreach (var item in items)
        {
            Console.WriteLine($"# {item.FilePath}:{item.Id}");
            Console.WriteLine();
            Console.WriteLine($"**File:** {item.FilePath}");
            Console.WriteLine($"**Summary:** {item.Summary}");
            Console.WriteLine($"**Severity:** {item.Severity}");
            Console.WriteLine($"**Tags:** {string.Join(", ", item.Tags)}");
            Console.WriteLine($"**Analysis Date:** {item.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine();

            if (!string.IsNullOrEmpty(item.ContentError))
            {
                Console.WriteLine($"**Error:** {item.ContentError}");
            }
            else
            {
                Console.WriteLine("## Detailed Analysis");
                Console.WriteLine();
                Console.WriteLine(item.Content);
            }

            Console.WriteLine();
            Console.WriteLine("---");
            Console.WriteLine();
        }
    }

    private static void OutputJsonFormat(List<DebtItemWithContent> items)
    {
        var json = JsonSerializer.Serialize(items, s_jsonOptions);
        Console.WriteLine(json);
    }

    private static void OutputXmlFormat(List<DebtItemWithContent> items)
    {
        var doc = new XmlDocument();
        var root = doc.CreateElement("DebtItems");
        doc.AppendChild(root);

        foreach (var item in items)
        {
            var itemElement = doc.CreateElement("DebtItem");
            itemElement.SetAttribute("id", item.Id);
            itemElement.SetAttribute("severity", item.Severity.ToString());
            itemElement.SetAttribute("filePath", item.FilePath);
            itemElement.SetAttribute("timestamp", item.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"));

            var summaryElement = doc.CreateElement("Summary");
            summaryElement.InnerText = item.Summary;
            itemElement.AppendChild(summaryElement);

            var tagsElement = doc.CreateElement("Tags");
            foreach (var tag in item.Tags)
            {
                var tagElement = doc.CreateElement("Tag");
                tagElement.InnerText = tag.ToString();
                tagsElement.AppendChild(tagElement);
            }
            itemElement.AppendChild(tagsElement);

            var contentElement = doc.CreateElement("Content");
            if (!string.IsNullOrEmpty(item.ContentError))
            {
                contentElement.SetAttribute("error", item.ContentError);
            }
            else
            {
                var contentData = doc.CreateCDataSection(item.Content);
                contentElement.AppendChild(contentData);
            }
            itemElement.AppendChild(contentElement);

            root.AppendChild(itemElement);
        }

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            OmitXmlDeclaration = false,
        };

        using var writer = XmlWriter.Create(Console.Out, settings);
        doc.WriteTo(writer);
        writer.Flush();
        Console.WriteLine();
    }

    private static bool IsNonInteractiveMode(Settings settings)
    {
        return settings.PlainOutput || settings.JsonOutput || settings.XmlOutput;
    }

    private async Task<List<DebtItemWithContent>> LoadContentForAllItems(
        List<DebtItemWithFile> debtItems
    )
    {
        var itemsWithContent = new List<DebtItemWithContent>();
        var totalItems = debtItems.Count;
        var currentItem = 0;

        foreach (
            var item in debtItems
                .OrderByDescending(i => i.DebtItem.Severity)
                .ThenBy(i => i.FilePath)
        )
        {
            currentItem++;

            var contentItem = new DebtItemWithContent
            {
                Id = item.DebtItem.Id,
                Summary = item.DebtItem.Summary,
                Severity = item.DebtItem.Severity,
                Tags = item.DebtItem.Tags,
                FilePath = item.FilePath,
                Timestamp = item.DebtItem.Reference.Timestamp,
            };

            try
            {
                var detailedContent = await techDebtStorage.LoadTechDebtAsync(
                    item.DebtItem.Reference
                );
                contentItem.Content = detailedContent ?? "";

                if (string.IsNullOrWhiteSpace(contentItem.Content))
                {
                    contentItem.ContentError = "Content not available or empty";
                }
            }
            catch (Exception ex)
            {
                contentItem.ContentError = $"Error loading content: {ex.Message}";
            }

            itemsWithContent.Add(contentItem);
        }

        return itemsWithContent;
    }

    private sealed class DebtItemWithFile
    {
        public TechnicalDebtItem DebtItem { get; set; } = null!;
        public string FilePath { get; set; } = string.Empty;
    }

    private sealed class DebtItemOption
    {
        public string DisplayText { get; set; } = string.Empty;
        public TechnicalDebtItem DebtItem { get; set; } = null!;
        public string FilePath { get; set; } = string.Empty;
    }

    private sealed class DebtItemWithContent
    {
        public string Id { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public DebtSeverity Severity { get; set; }
        public DebtTag[] Tags { get; set; } = [];
        public string FilePath { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ContentError { get; set; }
    }

    public class Settings : CommandSettings
    {
        [Description(
            "Path to the repository (optional, uses default.repository or current directory)"
        )]
        [CommandArgument(0, "[REPOSITORY_PATH]")]
        public string? RepositoryPath { get; init; }

        [Description(
            "Regex pattern to include files (only debt items from files matching this pattern will be shown)"
        )]
        [CommandOption("--include")]
        public string? IncludePattern { get; init; }

        [Description(
            "Regex pattern to exclude files (debt items from files matching this pattern will be hidden)"
        )]
        [CommandOption("--exclude")]
        public string? ExcludePattern { get; init; }

        [Description("Filter by specific severity level (Critical, High, Medium, Low)")]
        [CommandOption("--severity")]
        public DebtSeverity? SeverityFilter { get; init; }

        [Description("Filter by specific debt tag (CodeSmell, Naming, Performance, etc.)")]
        [CommandOption("--tag")]
        public DebtTag? TagFilter { get; init; }

        [Description("Output as plain markdown format including all debt item data and content")]
        [CommandOption("--plain")]
        public bool PlainOutput { get; init; }

        [Description("Output as JSON format including all debt item data and content")]
        [CommandOption("--json")]
        public bool JsonOutput { get; init; }

        [Description("Output as XML format including all debt item data and content")]
        [CommandOption("--xml")]
        public bool XmlOutput { get; init; }

        [Description(
            "View specific debt item by file path and debt ID (format: '<pathToFile>:debtId')"
        )]
        [CommandOption("--id")]
        public string? DebtId { get; init; }

        [Description("Enable interactive mode to browse through items and return to selection")]
        [CommandOption("-i|--interactive")]
        public bool InteractiveMode { get; init; }
    }
}
