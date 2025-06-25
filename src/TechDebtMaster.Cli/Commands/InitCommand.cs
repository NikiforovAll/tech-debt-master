using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace TechDebtMaster.Cli.Commands;

[Description("Initialize TechDebtMaster in the current repository")]
public class InitCommand : AsyncCommand<InitCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var currentDirectory = Directory.GetCurrentDirectory();
        var vscodeDir = Path.Combine(currentDirectory, ".vscode");
        var mcpJsonPath = Path.Combine(vscodeDir, "mcp.json");
        var gitHubPromptsDir = Path.Combine(currentDirectory, ".github", "prompts");
        var promptFilePath = Path.Combine(gitHubPromptsDir, "tdm-work-on-debt.prompt.md");
        var gitIgnorePath = Path.Combine(currentDirectory, ".gitignore");

        try
        {
            var isVscodeProfile = string.Equals(
                settings.Profile,
                "vscode",
                StringComparison.OrdinalIgnoreCase
            );

            if (isVscodeProfile)
            {
                // Check if mcp.json already exists
                if (File.Exists(mcpJsonPath) && !settings.Force)
                {
                    AnsiConsole.MarkupLine(
                        "[yellow]Warning:[/] .vscode/mcp.json already exists. Use --force to overwrite."
                    );
                    return 1;
                }

                // Create .vscode directory and mcp.json configuration
                Directory.CreateDirectory(vscodeDir);
                await CreateMcpConfigurationAsync(mcpJsonPath);
                AnsiConsole.MarkupLine("[green]✓[/] Created .vscode/mcp.json configuration");

                // Create .github/prompts directory and prompt file
                Directory.CreateDirectory(gitHubPromptsDir);
                await CreatePromptFileAsync(promptFilePath);
                AnsiConsole.MarkupLine(
                    "[green]✓[/] Created .github/prompts/tdm-work-on-debt.prompt.md"
                );
            }

            // Always update .gitignore to include .tdm folder
            await UpdateGitIgnoreAsync(gitIgnorePath);
            AnsiConsole.MarkupLine("[green]✓[/] Updated .gitignore to include .tdm folder");

            AnsiConsole.MarkupLine("[green]✓[/] TechDebtMaster initialization complete!");

            if (isVscodeProfile)
            {
                AnsiConsole.MarkupLine("[dim]You can now start the MCP server with:[/] tdm mcp");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Failed to initialize TechDebtMaster: {ex.Message}"
            );
            return 1;
        }
    }

    private static async Task CreateMcpConfigurationAsync(string mcpJsonPath)
    {
        var mcpConfig = new
        {
            servers = new { techdebtmaster = new { url = "http://localhost:3001" } },
        };

        var options = new JsonSerializerOptions { WriteIndented = true };

        var jsonContent = JsonSerializer.Serialize(mcpConfig, options);
        await File.WriteAllTextAsync(mcpJsonPath, jsonContent);
    }

    private static async Task CreatePromptFileAsync(string promptFilePath)
    {
        var promptContent = """
# Technical Debt Workflow

**Mode:** agent  
**Tools:** changes, codebase, editFiles, fetch, findTestFiles, problems, runCommands, runTasks, search, searchResults, terminalLastCommand, terminalSelection, testFailure, usages, tdm-get-item, tdm-list-items, tdm-remove-item, tdm-show-repo-stats  
**Description:** An autonomous workflow for identifying, analyzing, and resolving technical debt in a codebase to improve maintainability and efficiency.

## Workflow

Execute the following workflow to systematically address technical debt:

### 1. Assessment Phase
- Use 'tdm-show-repo-stats' to gather repository-wide technical debt metrics
- Review debt distribution across files, types, and severity levels
- Document initial findings for reference

### 2. Prioritization Phase
- Use 'tdm-list-items' to retrieve first page of technical debt items (they are already sorted by priority by default)
- Present a user with an items as markdown table
- Don't use `tdm-get-item` yet

### 3. Resolution Phase (ONE BY ONE)
- Use 'tdm-get-item' to fetch detailed item information
- Present user with the item
- Analyze item validity:
  - Review related code
  - Verify if debt is still relevant
  - Document investigation findings
- For each valid item:
  - Implement necessary fixes
- Remove resolved items using 'tdm-remove-item'
- Complete ALL debt items in current file before proceeding

### 4. Validation Requirements
- Ensure all changes maintain existing functionality
- Document any architectural decisions
- Request human review for complex changes

## Constraints

Request clarification when:
- Item description is ambiguous
- Multiple resolution approaches exist
- Implementation impact is unclear
- Dependencies affect other components

Use emojis where appropriate:
- ✅ for completed tasks
- ❗ for issues or blockers
- 📄 for documentation updates

- Once item is resolved or if it is not relevant anymore, remove it from the list using 'tdm-remove-item'. 
- Ask user for confirmation before removing.
- Ask user before starting the next item.
""";

        await File.WriteAllTextAsync(promptFilePath, promptContent);
    }

    private static async Task UpdateGitIgnoreAsync(string gitIgnorePath)
    {
        var gitIgnoreContent = string.Empty;

        if (File.Exists(gitIgnorePath))
        {
            gitIgnoreContent = await File.ReadAllTextAsync(gitIgnorePath);
        }

        // Check if .tdm is already in .gitignore
        if (!gitIgnoreContent.Contains(".tdm"))
        {
            // Add .tdm to .gitignore
            if (!gitIgnoreContent.EndsWith('\n') && !string.IsNullOrEmpty(gitIgnoreContent))
            {
                gitIgnoreContent += Environment.NewLine;
            }

            gitIgnoreContent += "# TechDebtMaster analysis directory" + Environment.NewLine;
            gitIgnoreContent += ".tdm" + Environment.NewLine;

            await File.WriteAllTextAsync(gitIgnorePath, gitIgnoreContent);
        }
    }

    public class Settings : CommandSettings
    {
        [Description("Force overwrite existing files")]
        [CommandOption("-f|--force")]
        public bool Force { get; init; }

        [Description("Profile to initialize (vscode)")]
        [CommandOption("-p|--profile")]
        public string? Profile { get; init; }
    }
}
