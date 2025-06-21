using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Commands;
using TechDebtMaster.Cli.Services;

var services = new ServiceCollection();
services.ConfigureServices();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

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

    config
        .AddCommand<AnalyzeCommand>("analyze")
        .WithDescription("Analyze a repository for technical debt")
        .WithExample("analyze", "/home/user/my-repo");

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
});

return await app.RunAsync(args);
