using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace TechDebtMaster.Cli.Commands;

public class CapabilitiesCommand : Command<CapabilitiesCommand.Settings>
{
    public override int Execute(CommandContext context, Settings settings)
    {
        // Render ASCII art title
        var figlet = new FigletText("TechDebtMaster").Centered().Color(Color.Green);
        AnsiConsole.Write(figlet);

        // Add a divider
        AnsiConsole.Write(new Rule().RuleStyle("grey30"));
        AnsiConsole.WriteLine();

        // Display version
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        AnsiConsole.MarkupLine($"[dim]Version {version}[/]");
        AnsiConsole.WriteLine();

        // Main question and answer
        AnsiConsole.MarkupLine("[bold yellow]What can TechDebtMaster do?[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine(
            "[cyan]TechDebtMaster is your AI-powered companion for managing technical debt in your codebase.[/]"
        );
        AnsiConsole.WriteLine();

        // Core capabilities
        AnsiConsole.MarkupLine("[bold underline]🔍 Core Capabilities:[/]");
        AnsiConsole.WriteLine();

        var capabilities = new List<(string Icon, string Title, string Description)>
        {
            ("📊", "Smart Code Analysis", "Analyze your entire codebase using AI to identify technical debt, code smells, and improvement opportunities"),
            ("🎯", "Prioritized Insights", "Get severity-based rankings of issues, helping you focus on what matters most"),
            ("📱", "Interactive Reports", "Generate beautiful HTML reports you can share with your team and modify directly"),
            ("🔄", "Change Tracking", "Monitor how your technical debt evolves over time with intelligent file change detection"),
            ("🏷️", "Categorized Issues", "Organize findings by tags like Performance, Security, Code Smells, and more"),
            ("🛠️", "Developer Integration", "Works seamlessly with VS Code and other tools through MCP (Model Context Protocol)"),
            ("⚙️", "Flexible Configuration", "Customize analysis patterns, AI models, and output formats to match your workflow"),
            ("🎨", "Template Management", "Use built-in or custom prompt templates to tailor analysis to your specific needs")
        };

        foreach (var (icon, title, description) in capabilities)
        {
            AnsiConsole.MarkupLine($"[green]{icon}[/] [bold]{title}[/]");
            AnsiConsole.MarkupLine($"   {description}");
            AnsiConsole.WriteLine();
        }

        // Workflow overview
        AnsiConsole.MarkupLine("[bold underline]🚀 How It Works:[/]");
        AnsiConsole.WriteLine();

        var workflow = new List<(string Step, string Command, string Description)>
        {
            ("1️⃣", "tdm init", "Initialize TechDebtMaster in your repository"),
            ("2️⃣", "tdm repo index", "Scan and index your codebase for analysis"),
            ("3️⃣", "tdm debt analyze", "Let AI analyze your code for technical debt"),
            ("4️⃣", "tdm debt show", "View results in an organized tree structure"),
            ("5️⃣", "tdm debt report", "Generate shareable HTML reports")
        };

        foreach (var (step, command, description) in workflow)
        {
            AnsiConsole.MarkupLine($"[yellow]{step}[/] [blue]{command}[/] - {description}");
        }

        AnsiConsole.WriteLine();

        // Integration capabilities
        AnsiConsole.MarkupLine("[bold underline]🔌 Integration & Automation:[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("• [green]MCP Server[/] - Start with '[blue]tdm mcp[/]' to enable external tool integration");
        AnsiConsole.MarkupLine("• [green]VS Code Integration[/] - Use '[blue]tdm init --profile vscode[/]' for seamless editor integration");
        AnsiConsole.MarkupLine("• [green]CI/CD Ready[/] - Perfect for automated code quality checks in your pipeline");
        AnsiConsole.MarkupLine("• [green]Team Collaboration[/] - Share and modify HTML reports with your team");
        AnsiConsole.WriteLine();

        // Supported technologies
        AnsiConsole.MarkupLine("[bold underline]💻 What Languages & Technologies?[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("TechDebtMaster works with [green]any programming language[/] because it uses:");
        AnsiConsole.MarkupLine("• [cyan]AI-powered analysis[/] that understands code patterns across languages");
        AnsiConsole.MarkupLine("• [cyan]Flexible file filtering[/] to focus on specific file types or patterns");
        AnsiConsole.MarkupLine("• [cyan]Customizable prompts[/] that can be tailored to your technology stack");
        AnsiConsole.WriteLine();

        // Getting started
        AnsiConsole.MarkupLine("[bold underline]🎯 Ready to Start?[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("Try these commands to get started:");
        AnsiConsole.MarkupLine("• [blue]tdm help[/] - See detailed usage examples");
        AnsiConsole.MarkupLine("• [blue]tdm init[/] - Initialize in your current repository");
        AnsiConsole.MarkupLine("• [blue]tdm config show[/] - View current configuration");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[dim]TechDebtMaster: Making technical debt management simple, actionable, and collaborative.[/]");

        return 0;
    }

    public class Settings : CommandSettings { }
}