using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Commands;
using TechDebtMaster.Cli.Services;

var services = new ServiceCollection();
services.ConfigureServices();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

var config = new ConfigurationService();
await config.EnsureDefaultsAsync();

// Ensure templates are copied to user directory
var templateService = new TemplateService();
await templateService.EnsureTemplatesAsync();

// Ensure default walkthrough is available
var walkthroughService = new WalkthroughService();
await walkthroughService.EnsureDefaultWalkthroughAsync();

// Set the default command to show welcome screen
app.SetDefaultCommand<DefaultCommand>();

// Get configuration to determine which commands to register
var appConfig = config.GetConfiguration();

app.Configure(config =>
{
    // Configure exception handling
    config.SetExceptionHandler(
        (ex, _) =>
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return -99;
        }
    );

    config.AddBranch(
        "repo",
        branch =>
        {
            branch.SetDescription("Repository management and indexing operations");

            branch
                .AddCommand<AnalyzeIndexCommand>("index")
                .WithDescription("Index repostitory content")
                .WithExample("repo", "index")
                .WithExample("repo", "index", "/home/user/my-repo")
                .WithExample("repo", "index", "/home/user/my-repo", "--include", "\\.cs$");

            branch
                .AddCommand<AnalyzeStatusCommand>("status")
                .WithDescription("Show status of previous analysis and repository changes")
                .WithExample("repo", "status")
                .WithExample("repo", "status", "/home/user/my-repo");
        }
    );

    config.AddBranch(
        "debt",
        branch =>
        {
            branch.SetDescription("Technical debt analysis and reporting");

            branch
                .AddCommand<AnalyzeDebtCommand>("analyze")
                .WithDescription("Perform debt analysis on all indexed files")
                .WithExample("debt", "analyze")
                .WithExample("debt", "analyze", "/home/user/my-repo")
                .WithExample("debt", "analyze", "/home/user/my-repo", "--latest");

            branch
                .AddCommand<AnalyzeShowCommand>("show")
                .WithDescription(
                    "Show technical debt statistics in a tree structure grouped by tags"
                )
                .WithExample("debt", "show")
                .WithExample("debt", "show", "/home/user/my-repo")
                .WithExample("debt", "show", "/home/user/my-repo", "--severity", "High")
                .WithExample("debt", "show", "/home/user/my-repo", "--tag", "Performance");

            branch
                .AddCommand<AnalyzeViewCommand>("view")
                .WithDescription("View detailed content of specific technical debt items")
                .WithExample("debt", "view")
                .WithExample("debt", "view", "/home/user/my-repo")
                .WithExample("debt", "view", "/home/user/my-repo", "--severity", "Critical")
                .WithExample("debt", "view", "/home/user/my-repo", "--tag", "Performance");

            branch
                .AddCommand<DebtReportCommand>("report")
                .WithDescription("Generate an interactive HTML report of technical debt")
                .WithExample("debt", "report")
                .WithExample("debt", "report", "--output", "my-report.html")
                .WithExample(
                    "debt",
                    "report",
                    "/home/user/my-repo",
                    "--output",
                    "report.html",
                    "--open"
                )
                .WithExample(
                    "debt",
                    "report",
                    "--include",
                    "\\.cs$",
                    "--output",
                    "csharp-debt.html"
                );

            branch
                .AddCommand<DebtImportCommand>("import")
                .WithDescription("Import modified HTML report to update analysis data")
                .WithExample("debt", "import", "modified-report.html")
                .WithExample("debt", "import", "modified-report.html", "--apply")
                .WithExample(
                    "debt",
                    "import",
                    "modified-report.html",
                    "--repo",
                    "/path/to/repo",
                    "--verbose"
                );
        }
    );

    config
        .AddCommand<InitCommand>("init")
        .WithDescription("Initialize TechDebtMaster in the current repository")
        .WithExample("init")
        .WithExample("init", "--profile", "vscode")
        .WithExample("init", "--force");

    config
        .AddCommand<CleanCommand>("clean")
        .WithDescription("Remove the .tdm folder from the current directory");

    config
        .AddCommand<McpServerCommand>("mcp")
        .WithDescription("Start MCP (Model Context Protocol) server")
        .WithExample("mcp")
        .WithExample("mcp", "/path/to/repo")
        .WithExample("mcp", "--port", "3001");

    config
        .AddCommand<HelpCommand>("help")
        .WithDescription("Show detailed help with usage examples and workflows");

    config
        .AddCommand<WalkthroughCommand>("walkthrough")
        .WithDescription("Open the TechDebtMaster product walkthrough in your browser")
        .WithExample("walkthrough");

    config.AddBranch(
        "config",
        branch =>
        {
            branch.SetDescription("Manage configuration settings");

            branch
                .AddCommand<ConfigShowCommand>("show")
                .WithDescription("Display current configuration");
            branch
                .AddCommand<ConfigSetCommand>("set")
                .WithDescription("Set a configuration value")
                .WithExample("config", "set", "ai.key", "<your-api-key>");
        }
    );

    // Only register DIAL commands if provider is DIAL
    if (appConfig.Provider.Equals("dial", StringComparison.OrdinalIgnoreCase))
    {
        config.AddBranch(
            "dial",
            branch =>
            {
                branch.SetDescription("DIAL API operations");

                branch.AddBranch(
                    "models",
                    modelsBranch =>
                    {
                        modelsBranch.SetDescription("Model management operations");

                        modelsBranch
                            .AddCommand<DialModelsListCommand>("list")
                            .WithDescription("List all available DIAL models");
                        modelsBranch
                            .AddCommand<DialModelsSetDefaultCommand>("set-default")
                            .WithDescription(
                                "Set the default model for AI operations (interactive selection)"
                            )
                            .WithExample("dial", "models", "set-default");
                    }
                );

                branch
                    .AddCommand<DialLimitsCommand>("limits")
                    .WithDescription("Get token limits for a specific model")
                    .WithExample("dial", "limits", "gpt-4o-mini-2024-07-18");
            }
        );
    }

    config.AddBranch(
        "prompts",
        branch =>
        {
            branch.SetDescription("Manage prompt templates");

            branch
                .AddCommand<PromptsEditCommand>("edit")
                .WithDescription("Edit a prompt template")
                .WithExample("prompts", "edit");

            branch
                .AddCommand<PromptsRestoreTemplatesCommand>("restore")
                .WithDescription("Restore prompt templates to default state")
                .WithExample("prompts", "restore");

            branch
                .AddCommand<PromptsSetDefaultCommand>("set-default")
                .WithDescription("Set the default prompt template")
                .WithExample("prompts", "set-default");
        }
    );
});

return await app.RunAsync(args);
