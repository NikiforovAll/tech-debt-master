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
        var rootMcpJsonPath = Path.Combine(currentDirectory, ".mcp.json");
        var gitHubPromptsDir = Path.Combine(currentDirectory, ".github", "prompts");
        var promptFilePath = Path.Combine(gitHubPromptsDir, "tdm-work-on-debt.prompt.md");
        var gitIgnorePath = Path.Combine(currentDirectory, ".gitignore");
        var geminiDir = Path.Combine(currentDirectory, ".gemini");
        var geminiSettingsPath = Path.Combine(geminiDir, "settings.json");
        var geminiPromptsDir = Path.Combine(geminiDir, "prompts");
        var geminiPromptPath = Path.Combine(geminiPromptsDir, "techdebt-resolution.md");

        try
        {
            var isVscodeProfile = string.Equals(
                settings.Profile,
                "vscode",
                StringComparison.OrdinalIgnoreCase
            );

            var isClaudeProfile = string.Equals(
                settings.Profile,
                "claude",
                StringComparison.OrdinalIgnoreCase
            );

            var isGeminiProfile = string.Equals(
                settings.Profile,
                "gemini",
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
            else if (isClaudeProfile)
            {
                // Check if .mcp.json already exists
                if (File.Exists(rootMcpJsonPath) && !settings.Force)
                {
                    AnsiConsole.MarkupLine(
                        "[yellow]Warning:[/] .mcp.json already exists. Use --force to overwrite."
                    );
                    return 1;
                }

                // Create .mcp.json configuration in root directory
                await CreateClaudeMcpConfigurationAsync(rootMcpJsonPath);
                AnsiConsole.MarkupLine("[green]✓[/] Created .mcp.json configuration");
            }
            else if (isGeminiProfile)
            {
                // Check if .gemini/settings.json already exists
                if (File.Exists(geminiSettingsPath) && !settings.Force)
                {
                    AnsiConsole.MarkupLine(
                        "[yellow]Warning:[/] .gemini/settings.json already exists. Use --force to overwrite."
                    );
                    return 1;
                }

                // Create .gemini directory and settings.json configuration
                Directory.CreateDirectory(geminiDir);
                await CreateGeminiConfigurationAsync(geminiSettingsPath);
                AnsiConsole.MarkupLine("[green]✓[/] Created .gemini/settings.json configuration");

                // Create .gemini/prompts directory and prompt file
                Directory.CreateDirectory(geminiPromptsDir);
                await CreateGeminiPromptFileAsync(geminiPromptPath);
                AnsiConsole.MarkupLine("[green]✓[/] Created .gemini/prompts/techdebt-resolution.md");
            }

            // Always update .gitignore to include .tdm folder
            await UpdateGitIgnoreAsync(gitIgnorePath);
            AnsiConsole.MarkupLine("[green]✓[/] Updated .gitignore to include .tdm folder");

            AnsiConsole.MarkupLine("[green]✓[/] TechDebtMaster initialization complete!");

            if (isVscodeProfile || isClaudeProfile || isGeminiProfile)
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

    private static async Task CreateClaudeMcpConfigurationAsync(string mcpJsonPath)
    {
        var mcpConfig = new
        {
            mcpServers = new
            {
                techdebtmaster = new { type = "http", url = "http://127.0.0.1:3001" },
            },
        };

        var options = new JsonSerializerOptions { WriteIndented = true };

        var jsonContent = JsonSerializer.Serialize(mcpConfig, options);
        await File.WriteAllTextAsync(mcpJsonPath, jsonContent);
    }

    private static async Task CreateGeminiConfigurationAsync(string geminiSettingsPath)
    {
        var geminiConfig = new
        {
            mcpServers = new
            {
                techdebtmaster = new { httpUrl = "http://127.0.0.1:3001" },
            },
        };

        var options = new JsonSerializerOptions { WriteIndented = true };

        var jsonContent = JsonSerializer.Serialize(geminiConfig, options);
        await File.WriteAllTextAsync(geminiSettingsPath, jsonContent);
    }

    private static async Task CreatePromptFileAsync(string promptFilePath)
    {
        var promptContent = """
            ---
            mode: agent
            tools: ['changes', 'codebase', 'editFiles', 'fetch', 'findTestFiles', 'problems', 'runCommands', 'runTasks', 'search', 'searchResults', 'terminalLastCommand', 'terminalSelection', 'testFailure', 'usages', 'tdm-get-item', 'tdm-list-items', 'tdm-remove-item', 'tdm-show-repo-stats']
            description: 'An autonomous workflow for identifying, analyzing, and resolving technical debt in a codebase to improve maintainability and efficiency.'
            ---

            ## Workflow

            Execute the following workflow to systematically address technical debt:

            ### 1. Assessment Phase
            - Use `tdm-show-repo-stats` to gather repository-wide technical debt metrics
            - Review debt distribution across files, types, and severity levels
            - Document initial findings for reference

            ### 2. Prioritization Phase
            - Use `tdm-list-items` to retrieve first page of technical debt items (they are already sorted by priority by default)
            - Do not fetch all items at once, only the first page (max 5 items)
            - Present a user with an items as markdown table
            - Don't use `tdm-get-item` yet

            ### 3. Verification Phase
            - For each item in the list:
            - Use `tdm-get-item` to fetch detailed information about the item
            - Present the item to the user for review
            - Ask user for confirmation to proceed with the item

            ### 4. Resolution Phase
            - Use `tdm-get-item` to fetch detailed item information
            - Present user with the item
            - Analyze item validity:
            - Review related code
            - Verify if debt is still relevant
            - Document investigation findings
            - For each valid item:
            - Implement necessary fixes
            - Remove resolved items using `tdm-remove-item`
            - Complete ALL debt items in current file before proceeding

            ### 5. Validation Requirements
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
            - 📊 Assessment Phase
            - 📋 Prioritization Phase

            - Once item is resolved or if it is not relevant anymore, remove it from the list using 'tdm-remove-item'. 
            - Ask user for confirmation before removing.
            - Ask user before starting the next item.
            """;

        await File.WriteAllTextAsync(promptFilePath, promptContent);
    }

    private static async Task CreateGeminiPromptFileAsync(string promptFilePath)
    {
        var promptContent = """
            # Technical Debt Resolution Assistant

            You are an AI assistant specialized in analyzing and resolving technical debt in software projects using TechDebtMaster (tdm) commands.

            ## Available MCP Tools

            You have access to the following TechDebtMaster tools through MCP:

            ### 1. tdm-get-item
            - **Purpose**: Get a specific technical debt item by its ID
            - **Format**: ID is in the format 'filePath:id'
            - **Returns**: Markdown description of the item

            ### 2. tdm-show-repo-stats
            - **Purpose**: Get comprehensive technical debt statistics
            - **Includes**: Tag distribution, severity distribution, and file analysis counts
            - **Use**: For initial assessment and progress tracking

            ### 3. tdm-list-items
            - **Purpose**: Get a list of technical debt issues across all files
            - **Options**: Filter by pattern, severity, and tags
            - **Note**: Items are pre-sorted by priority

            ### 4. tdm-remove-item
            - **Purpose**: Remove a specific technical debt item by its ID
            - **Action**: Deletes both the debt item metadata and its associated content file
            - **Use**: After resolving a debt item

            ## Workflow for Resolving Technical Debt

            ### Phase 1: Assessment 📊
            1. Use `tdm-show-repo-stats` to understand the overall debt landscape
            2. Review the distribution of debt by type, severity, and files
            3. Document initial findings

            ### Phase 2: Prioritization 📋
            1. Use `tdm-list-items` to get the first page of items (max 5 items)
            - Items are already sorted by priority
            - Don't fetch all items at once
            2. Present items in a clear markdown table format
            3. Focus on high-priority items first

            ### Phase 3: Analysis and Resolution ✅
            For each technical debt item:

            1. **Fetch Details**: Use `tdm-get-item` with the item ID
            2. **Analyze**: 
            - Review the related code
            - Verify if the debt is still relevant
            - Understand the context and impact
            3. **Implement Fix**:
            - Make necessary code changes
            - Ensure functionality is maintained
            - Follow existing code conventions
            4. **Remove Item**: Use `tdm-remove-item` after successful resolution
            5. **Document**: Note any important decisions or changes

            ### Phase 4: Validation ❗
            - Ensure all changes maintain existing functionality
            - Run tests if available
            - Request human review for complex changes

            ## Best Practices

            1. **Work File by File**: Complete all debt items in one file before moving to the next
            2. **Ask for Confirmation**: 
            - When multiple resolution approaches exist
            3. **Clear Communication**:
            - Present findings clearly
            - Explain proposed solutions
            - Document any blockers or concerns

            ## Important Notes

            - If an item description is ambiguous, ask for clarification
            - If dependencies affect other components, discuss the impact
            - If the debt is no longer relevant (e.g., code has been refactored), still remove it from tracking
            - Always maintain code quality and follow project conventions

            ## Example Interaction Flow

            1. "Let me analyze the technical debt in this repository..."
            → Use `tdm-show-repo-stats`

            2. "I found X total debt items. Let me look at the highest priority ones..."
            → Use `tdm-list-items` (first page only)

            3. "Let me examine this item in detail..."
            → Use `tdm-get-item` with the specific ID

            4. "I've resolved this issue by [explanation]. Shall I remove it from tracking?"
            → Use `tdm-remove-item` after confirmation

            Remember: The goal is systematic, thorough resolution of technical debt while maintaining code quality and functionality.
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

        [Description("Profile to initialize (vscode, claude, gemini)")]
        [CommandOption("-p|--profile")]
        public string? Profile { get; init; }
    }
}
