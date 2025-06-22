using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

public class DialModelsListCommand(IDialService dialService) : AsyncCommand
{
    private static readonly string[] s_colors =
    [
        "red",
        "green",
        "blue",
        "yellow",
        "magenta",
        "cyan",
        "orange1",
        "purple",
        "lime",
        "deeppink1",
        "darkturquoise",
        "gold1",
        "hotpink",
        "springgreen1",
        "mediumpurple",
        "lightsalmon1",
        "plum1",
        "khaki1",
        "lightgreen",
    ];

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            var models = await AnsiConsole
                .Status()
                .StartAsync(
                    "Fetching models...",
                    async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Star);
                        ctx.SpinnerStyle(Style.Parse("green"));
                        return await dialService.GetModelsAsync();
                    }
                );

            if (models.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No models found.[/]");
                return 0;
            }

            var groupedModels = GroupModelsByPrefix(models);
            var tree = BuildModelTree(groupedModels);

            AnsiConsole.Write(tree);
            AnsiConsole.MarkupLine($"\n[green]Total models: {models.Count}[/]");

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to fetch models: {ex.Message}");
            return 1;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private static Dictionary<string, List<DialModel>> GroupModelsByPrefix(List<DialModel> models)
    {
        var grouped = new Dictionary<string, List<DialModel>>();

        foreach (var model in models.OrderBy(m => m.Id))
        {
            var prefix = ExtractPrefix(model.Id);

            if (!grouped.TryGetValue(prefix, out var modelList))
            {
                modelList = [];
                grouped[prefix] = modelList;
            }

            modelList.Add(model);
        }

        return grouped.OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Value);
    }

    private static string ExtractPrefix(string modelId)
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

    private static Tree BuildModelTree(Dictionary<string, List<DialModel>> groupedModels)
    {
        var tree = new Tree("[bold white]Models by Provider[/]");
        var random = new Random();
        var usedColors = new HashSet<string>();

        foreach (var group in groupedModels)
        {
            var color = GetUniqueColor(random, usedColors);
            var providerNode = tree.AddNode(
                $"[{color}]{group.Key}[/] ({group.Value.Count} models)"
            );

            foreach (var model in group.Value)
            {
                providerNode.AddNode($"[dim]{model.Id}[/]");
            }
        }

        return tree;
    }

    private static string GetUniqueColor(Random random, HashSet<string> usedColors)
    {
        var availableColors = s_colors.Where(c => !usedColors.Contains(c)).ToArray();

        if (availableColors.Length == 0)
        {
            usedColors.Clear();
            availableColors = s_colors;
        }

#pragma warning disable CA5394 // Do not use insecure randomness
        var selectedColor = availableColors[random.Next(availableColors.Length)];
#pragma warning restore CA5394 // Do not use insecure randomness
        usedColors.Add(selectedColor);

        return selectedColor;
    }
}
