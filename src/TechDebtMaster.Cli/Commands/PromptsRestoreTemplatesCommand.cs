using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

/// <summary>
/// Command to restore prompt templates from assembly to user directory
/// </summary>
[Description("Restore prompt templates to default state")]
public class PromptsRestoreTemplatesCommand(ITemplateService templateService)
    : AsyncCommand<PromptsRestoreTemplatesCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            AnsiConsole.MarkupLine("[blue]Restoring prompt templates (overriding existing)...[/]");

            var templatesDirectory = templateService.GetTemplatesDirectory();
            AnsiConsole.MarkupLine($"[dim]Templates directory:[/] {templatesDirectory}");

            // Force restore templates from assembly, overriding existing ones
            await templateService.ForceRestoreTemplatesAsync();

            var availableTemplates = await templateService.GetAvailableTemplatesAsync();
            var templateCount = availableTemplates.Count();

            if (templateCount > 0)
            {
                AnsiConsole.MarkupLine(
                    $"[green]✓ Successfully restored {templateCount} template(s):[/]"
                );

                foreach (var template in availableTemplates)
                {
                    AnsiConsole.MarkupLine($"  [dim]•[/] {template}");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No templates found in assembly to restore.[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error restoring templates:[/] {ex.Message}");
            return 1;
        }
    }

    public class Settings : CommandSettings
    {
        // No additional settings needed for this command
    }
}
