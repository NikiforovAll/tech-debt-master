using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

public class DialLimitsCommand(IDialService dialService, IConfigurationService configurationService)
    : AsyncCommand<DialLimitsSettings>
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        DialLimitsSettings settings
    )
    {
        ArgumentNullException.ThrowIfNull(settings);

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            var modelId = settings.ModelId;
            if (string.IsNullOrWhiteSpace(modelId))
            {
                var config = configurationService.GetConfiguration();
                modelId = config.Model;

                if (string.IsNullOrWhiteSpace(modelId))
                {
                    AnsiConsole.MarkupLine(
                        "[red]Error:[/] No model specified and no default model configured."
                    );
                    AnsiConsole.MarkupLine(
                        "Use '[blue]config set ai.model <MODEL_ID>[/]' to set a default model or provide a model ID as an argument."
                    );
                    return 1;
                }
            }

            var limits = await AnsiConsole
                .Status()
                .StartAsync(
                    $"Fetching limits for {modelId}...",
                    async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Star);
                        ctx.SpinnerStyle(Style.Parse("green"));
                        return await dialService.GetModelLimitsAsync(modelId);
                    }
                );

            if (limits == null)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]No limits found for model '[blue]{modelId}[/]' or model doesn't exist.[/]"
                );
                return 0;
            }

            if (settings.JsonOutput)
            {
                DisplayJsonOutput(limits, modelId);
            }
            else
            {
                DisplayTableOutput(limits, modelId);
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to fetch limits: {ex.Message}");
            return 1;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private static void DisplayJsonOutput(DialModelLimits limits, string modelId)
    {
        AnsiConsole.MarkupLine($"[green]Limits for model '[blue]{modelId}[/]':[/]");
        AnsiConsole.WriteLine();

        var json = JsonSerializer.Serialize(limits, s_jsonOptions);
        var jsonText = new JsonText(json);
        AnsiConsole.Write(jsonText);
    }

    private static void DisplayTableOutput(DialModelLimits limits, string modelId)
    {
        AnsiConsole.MarkupLine($"[green]Limits for model '[blue]{modelId}[/]':[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("[bold]Limit Type[/]");
        table.AddColumn("[bold]Total[/]");
        table.AddColumn("[bold]Used[/]");
        table.AddColumn("[bold]Remaining[/]");
        table.Border(TableBorder.Rounded);

        // Token limits
        AddTokenStatsRow(table, "Per Minute (Tokens)", limits.MinuteTokenStats);
        AddTokenStatsRow(table, "Per Day (Tokens)", limits.DayTokenStats);
        AddTokenStatsRow(table, "Per Week (Tokens)", limits.WeekTokenStats);
        AddTokenStatsRow(table, "Per Month (Tokens)", limits.MonthTokenStats);

        // Request limits
        AddRequestStatsRow(table, "Per Hour (Requests)", limits.HourRequestStats);
        AddRequestStatsRow(table, "Per Day (Requests)", limits.DayRequestStats);

        AnsiConsole.Write(table);

        // Check if any meaningful limits exist
        var hasLimits = HasAnyValidLimits(limits);
        if (!hasLimits)
        {
            AnsiConsole.MarkupLine(
                "\n[yellow]Note: This model may not be available to you or has no limits configured.[/]"
            );
        }
    }

    private static void AddTokenStatsRow(Table table, string limitType, TokenStats? stats)
    {
        if (stats == null)
        {
            table.AddRow(limitType, "[dim]N/A[/]", "[dim]N/A[/]", "[dim]N/A[/]");
            return;
        }

        var total = stats.Total;
        var used = stats.Used;

        if (total <= 0 || total == long.MaxValue)
        {
            table.AddRow(
                limitType,
                "[dim]No limit[/]",
                used.ToString("N0", CultureInfo.InvariantCulture),
                "[dim]No limit[/]"
            );
        }
        else
        {
            var remaining = Math.Max(0, total - used);
            table.AddRow(
                limitType,
                total.ToString("N0", CultureInfo.InvariantCulture),
                used.ToString("N0", CultureInfo.InvariantCulture),
                remaining.ToString("N0", CultureInfo.InvariantCulture)
            );
        }
    }

    private static void AddRequestStatsRow(Table table, string limitType, RequestStats? stats)
    {
        if (stats == null)
        {
            table.AddRow(limitType, "[dim]N/A[/]", "[dim]N/A[/]", "[dim]N/A[/]");
            return;
        }

        var total = stats.Total;
        var used = stats.Used;

        if (total <= 0 || total == long.MaxValue)
        {
            table.AddRow(
                limitType,
                "[dim]No limit[/]",
                used.ToString("N0", CultureInfo.InvariantCulture),
                "[dim]No limit[/]"
            );
        }
        else
        {
            var remaining = Math.Max(0, total - used);
            table.AddRow(
                limitType,
                total.ToString("N0", CultureInfo.InvariantCulture),
                used.ToString("N0", CultureInfo.InvariantCulture),
                remaining.ToString("N0", CultureInfo.InvariantCulture)
            );
        }
    }

    private static bool HasAnyValidLimits(DialModelLimits limits)
    {
        return (
                limits.MinuteTokenStats?.Total > 0 && limits.MinuteTokenStats.Total != long.MaxValue
            )
            || (limits.DayTokenStats?.Total > 0 && limits.DayTokenStats.Total != long.MaxValue)
            || (limits.WeekTokenStats?.Total > 0 && limits.WeekTokenStats.Total != long.MaxValue)
            || (limits.MonthTokenStats?.Total > 0 && limits.MonthTokenStats.Total != long.MaxValue)
            || (
                limits.HourRequestStats?.Total > 0 && limits.HourRequestStats.Total != long.MaxValue
            )
            || (limits.DayRequestStats?.Total > 0 && limits.DayRequestStats.Total != long.MaxValue);
    }
}

public class DialLimitsSettings : CommandSettings
{
    [Description(
        "The model ID to get limits for (optional, uses current model from config if not specified)"
    )]
    [CommandArgument(0, "[MODEL_ID]")]
    public string? ModelId { get; init; }

    [Description("Display the response as formatted JSON")]
    [CommandOption("--json")]
    public bool JsonOutput { get; init; }
}
