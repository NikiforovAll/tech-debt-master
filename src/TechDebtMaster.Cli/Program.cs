using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Commands;
using TechDebtMaster.Cli.Services;

var services = new ServiceCollection();
services.ConfigureServices();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

// Configure Spectre.Console capabilities for consistent Unicode support
// This ensures special characters (✓, ✗, ●, etc.) render correctly in packed versions
AnsiConsole.Profile.Capabilities.Unicode = true;

// Allow environment variable override for Unicode support
if (Environment.GetEnvironmentVariable("TDM_DISABLE_UNICODE") == "true")
{
    AnsiConsole.Profile.Capabilities.Unicode = false;
}

var config = new ConfigurationService();
await config.EnsureDefaultsAsync();

// Ensure templates are copied to user directory
var templateService = new TemplateService();
await templateService.EnsureTemplatesAsync();

// Set the default command to show welcome screen
app.SetDefaultCommand<DefaultCommand>();
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
        }
    );

    config
        .AddCommand<CleanCommand>("clean")
        .WithDescription("Remove the .tdm folder from the current directory");

    config
        .AddCommand<HelpCommand>("help")
        .WithDescription("Show detailed help with usage examples and workflows");

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
