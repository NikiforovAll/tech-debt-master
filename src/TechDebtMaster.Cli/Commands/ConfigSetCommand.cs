using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

[Description("Set a configuration value")]
public class ConfigSetCommand(IConfigurationService configurationService)
    : AsyncCommand<ConfigSetCommand.Settings>
{
    private static readonly HashSet<string> s_validKeys =
    [
        "ai.key",
        "ai.url",
        "ai.model",
        "ai.provider",
        "prompt.default",
        "default.repository",
        "default.include",
        "default.exclude",
    ];

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!s_validKeys.Contains(settings.Key))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid configuration key '{settings.Key}'.");
            AnsiConsole.MarkupLine("[yellow]Valid keys are:[/]");
            foreach (var key in s_validKeys)
            {
                AnsiConsole.MarkupLine($"  - {key}");
            }
            return 1;
        }

        if (settings.Key == "ai.provider")
        {
            var validProviders = new[] { "dial", "openai", "ollama" };
            if (!validProviders.Contains(settings.Value, StringComparer.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid provider '{settings.Value}'.");
                AnsiConsole.MarkupLine("[yellow]Valid providers are:[/]");
                foreach (var provider in validProviders)
                {
                    AnsiConsole.MarkupLine($"  - {provider}");
                }
                return 1;
            }
        }

        await configurationService.SetAsync(settings.Key, settings.Value);

        var displayValue = settings.Key.Contains("key", StringComparison.OrdinalIgnoreCase)
            ? MaskValue(settings.Value)
            : settings.Value;

        AnsiConsole.MarkupLine(
            $"[green]✓[/] Configuration value set: {settings.Key} = {displayValue}"
        );
        return 0;
    }

    private static string MaskValue(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= 8)
        {
            return "********";
        }
        return string.Concat(value.AsSpan(0, 4), "****", value.AsSpan()[^4..]);
    }

    public class Settings : CommandSettings
    {
        [Description("Configuration key (e.g., ai.key, ai.endpoint, ai.model)")]
        [CommandArgument(0, "<KEY>")]
        public string Key { get; init; } = string.Empty;

        [Description("Configuration value")]
        [CommandArgument(1, "<VALUE>")]
        public string Value { get; init; } = string.Empty;
    }
}
