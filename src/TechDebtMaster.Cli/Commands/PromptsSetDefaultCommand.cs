using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

/// <summary>
/// Command to set the default prompt template
/// </summary>
[Description("Set the default prompt template")]
public class PromptsSetDefaultCommand(
    ITemplateService templateService,
    IConfigurationService configurationService
) : AsyncCommand<PromptsSetDefaultCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var availableTemplates = await templateService.GetAvailableTemplatesAsync();
        var templateList = availableTemplates.ToList();

        if (templateList.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No prompt templates found.[/]");
            AnsiConsole.MarkupLine("[yellow]Run 'tdm analyze' first to initialize templates.[/]");
            return 1;
        }

        // Get current default if exists
        var currentDefault = await configurationService.GetAsync("prompt.default");
        var prompt = new SelectionPrompt<string>()
            .Title("Select a [green]prompt template[/] to set as default:")
            .PageSize(10)
            .AddChoices(templateList);

        if (!string.IsNullOrEmpty(currentDefault) && templateList.Contains(currentDefault))
        {
            prompt.HighlightStyle(new Style(foreground: Color.Yellow));
        }

        var selectedTemplate = AnsiConsole.Prompt(prompt);

        // Save the selected template as default
        await configurationService.SetAsync("prompt.default", selectedTemplate);

        AnsiConsole.MarkupLine(
            $"[green]✓[/] Default prompt template set to: [blue]{selectedTemplate}[/]"
        );
        return 0;
    }

    public class Settings : CommandSettings
    {
        // No additional settings needed for this command
    }
}
