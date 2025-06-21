using Microsoft.Extensions.DependencyInjection;
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

// Set the default command to show welcome screen
app.SetDefaultCommand<DefaultCommand>();
app.Configure(config =>
{
    // Configure exception handling
    config.PropagateExceptions();
    config.SetExceptionHandler(
        (ex, _) =>
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return -99;
        }
    );

    config.AddBranch(
        "analyze",
        branch =>
        {
            branch.SetDescription("Index and analyze repository for technical debt");
            branch.SetDefaultCommand<AnalyzeCommand>();

            branch
                .AddCommand<AnalyzeCommand>("index")
                .WithDescription("Index repository and analyze all files for technical debt")
                .WithExample("analyze index", "/home/user/my-repo")
                .WithExample("analyze index", "/home/user/my-repo", "--latest");

            branch
                .AddCommand<AnalyzeStatusCommand>("status")
                .WithDescription("Show status of previous analysis")
                .WithExample("analyze", "status")
                .WithExample("analyze", "status", "/home/user/my-repo");

            branch
                .AddCommand<AnalyzeDebtCommand>("debt")
                .WithDescription("Perform debt analysis on all indexed files")
                .WithExample("analyze", "debt")
                .WithExample("analyze", "debt", "/home/user/my-repo")
                .WithExample("analyze", "debt", "/home/user/my-repo", "--latest");

            branch
                .AddCommand<AnalyzeShowCommand>("show")
                .WithDescription(
                    "Show technical debt statistics in a tree structure grouped by tags"
                )
                .WithExample("analyze", "show")
                .WithExample("analyze", "show", "/home/user/my-repo")
                .WithExample("analyze", "show", "/home/user/my-repo", "--severity", "High")
                .WithExample("analyze", "show", "/home/user/my-repo", "--tag", "Performance");

            branch
                .AddCommand<AnalyzeViewCommand>("view")
                .WithDescription("View detailed content of specific technical debt items")
                .WithExample("analyze", "view")
                .WithExample("analyze", "view", "/home/user/my-repo")
                .WithExample("analyze", "view", "/home/user/my-repo", "--severity", "Critical")
                .WithExample("analyze", "view", "/home/user/my-repo", "--tag", "Performance");
        }
    );

    config
        .AddCommand<CleanCommand>("clean")
        .WithDescription("Remove the .tdm folder from the current directory");

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

            branch
                .AddCommand<DialListModelsCommand>("list-models")
                .WithDescription("List all available DIAL models");
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
