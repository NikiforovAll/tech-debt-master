using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

public class DialModelsSetDefaultCommand(
    IDialService dialService,
    IConfigurationService configService
) : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            // Fetch available models
            var models = await AnsiConsole
                .Status()
                .StartAsync(
                    "Fetching available models...",
                    async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Star);
                        ctx.SpinnerStyle(Style.Parse("yellow"));
                        return await dialService.GetModelsAsync();
                    }
                );

            if (models.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No models found.[/]");
                return 0;
            }

            // Get current default model for highlighting
            var currentConfig = configService.GetConfiguration();
            var currentDefaultModel = currentConfig.Model;

            // Group models by provider for better organization
            var groupedModels = GroupModelsByProvider(models);
            var modelChoices = new List<string>();

            // Build the selection list with provider grouping
            foreach (var group in groupedModels.OrderBy(g => g.Key))
            {
                foreach (var model in group.Value.OrderBy(m => m.Id))
                {
                    modelChoices.Add(model.Id); // Store actual ID for selection
                }
            }

            // Create selection prompt
            var selectedModel = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a [green]default model[/]:")
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up and down to reveal more models)[/]")
                    .AddChoices(models.Select(m => m.Id))
                    .UseConverter(modelId =>
                    {
                        return modelId == currentDefaultModel
                            ? $"{modelId} [dim](current default)[/]"
                            : modelId;
                    })
            );

            // Set the default model in configuration
            await configService.SetAsync("ai.model", selectedModel);

            AnsiConsole.MarkupLine($"[green]✓[/] Default model set to: [cyan]{selectedModel}[/]");
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to set default model: {ex.Message}");
            return 1;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private static Dictionary<string, List<DialModel>> GroupModelsByProvider(List<DialModel> models)
    {
        var grouped = new Dictionary<string, List<DialModel>>();

        foreach (var model in models)
        {
            var provider = ExtractProvider(model.Id);

            if (!grouped.TryGetValue(provider, out var modelList))
            {
                modelList = [];
                grouped[provider] = modelList;
            }

            modelList.Add(model);
        }

        return grouped;
    }

    private static string ExtractProvider(string modelId)
    {
        var knownPrefixes = new[]
        {
            "anthropic.",
            "azure-",
            "gemini-",
            "gpt-",
            "claude-",
            "amazon.",
            "stability.",
            "text-",
            "o1-",
            "o3-",
            "o4-",
            "rlab-",
            "DeepSeek-",
            "deepseek-",
            "dall-e-",
            "imagegeneration@",
        };

        var matchingPrefix = knownPrefixes.FirstOrDefault(prefix =>
            modelId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        );

        if (matchingPrefix != null)
        {
            return matchingPrefix.TrimEnd('.', '-', '@');
        }

        var dotIndex = modelId.IndexOf('.', StringComparison.Ordinal);
        var dashIndex = modelId.IndexOf('-', StringComparison.Ordinal);
        var atIndex = modelId.IndexOf('@', StringComparison.Ordinal);

        var separatorIndex = new[] { dotIndex, dashIndex, atIndex }
            .Where(i => i > 0)
            .DefaultIfEmpty(-1)
            .Min();

        return separatorIndex > 0 ? modelId[..separatorIndex] : "other";
    }
}
