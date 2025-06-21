using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

/// <summary>
/// Command to edit prompt templates
/// </summary>
[Description("Edit a prompt template")]
public class PromptsEditCommand(ITemplateService templateService, IEditorService editorService)
    : AsyncCommand<PromptsEditCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var availableTemplates = await templateService.GetAvailableTemplatesAsync();
            var templateList = availableTemplates.ToList();

            if (templateList.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No prompt templates found.[/]");
                AnsiConsole.MarkupLine(
                    "[yellow]Run 'tdm analyze' first to initialize templates.[/]"
                );
                return 1;
            }

            var selectedTemplate = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a [green]prompt template[/] to edit:")
                    .PageSize(10)
                    .AddChoices(templateList)
            );

            var templatePath = await templateService.GetTemplatePathAsync(
                $"{selectedTemplate}.prompty"
            );

            AnsiConsole.MarkupLine($"[blue]Opening template:[/] {selectedTemplate}");
            AnsiConsole.MarkupLine($"[dim]Path:[/] {templatePath}");

            await editorService.OpenFileAsync(templatePath);

            AnsiConsole.MarkupLine("[green]Template opened.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    public class Settings : CommandSettings
    {
        // No additional settings needed for this command
    }
}
