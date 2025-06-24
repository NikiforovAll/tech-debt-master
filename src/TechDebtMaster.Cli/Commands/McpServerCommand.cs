using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;
using TechDebtMaster.Cli.Tools;

namespace TechDebtMaster.Cli.Commands;

[Description("Start MCP (Model Context Protocol) server")]
public class McpServerCommand(IConfigurationService configurationService)
    : AsyncCommand<McpServerSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, McpServerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var repositoryPath = settings.RepositoryPath;
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            var defaultRepo = await configurationService.GetAsync("default.repository");
            repositoryPath = !string.IsNullOrWhiteSpace(defaultRepo)
                ? defaultRepo
                : Directory.GetCurrentDirectory();
        }

        AnsiConsole.MarkupLine($"[green]Starting MCP server for repository:[/] {repositoryPath}");
        AnsiConsole.MarkupLine($"[blue]Server will listen on:[/] http://localhost:{settings.Port}");
        AnsiConsole.MarkupLine("[yellow]Press Ctrl+C to stop the server[/]");
        AnsiConsole.WriteLine();

        try
        {
            var builder = WebApplication.CreateBuilder();
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Prevent immediate termination
                AnsiConsole.MarkupLine("[yellow]Shutting down server...[/]");
                cts.Cancel(); // Signal cancellation
            };

            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            // Configure the repository path in DI
            builder.Services.AddSingleton<IRepositoryPathProvider>(
                new RepositoryPathProvider(repositoryPath)
            );
            builder.Services.ConfigureServices();

            builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

            var app = builder.Build();

            // Configure to listen on the specified port
            app.Urls.Clear();
            app.Urls.Add($"http://localhost:{settings.Port}");

            app.MapMcp();

            AnsiConsole.MarkupLine("[green]✓[/] MCP server started successfully");

            await app.RunAsync(cts.Token);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error starting MCP server:[/] {ex.Message}");
            return 1;
        }
    }
}

public class McpServerSettings : CommandSettings
{
    [Description("Path to the repository (optional, uses default.repository or current directory)")]
    [CommandArgument(0, "[REPOSITORY_PATH]")]
    public string? RepositoryPath { get; init; }

    [Description("Port to listen on")]
    [CommandOption("-p|--port")]
    public int Port { get; init; } = 3001;
}
