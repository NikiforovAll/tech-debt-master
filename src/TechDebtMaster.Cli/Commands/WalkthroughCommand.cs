using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

public class WalkthroughCommand(IConfigurationService configurationService)
    : AsyncCommand<WalkthroughCommand.Settings>
{
    private const string HtmlTemplate =
        @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>TechDebtMaster - Product Walkthrough</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            line-height: 1.6;
            color: #1e293b;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
        }

        .container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 2rem;
        }

        .hero {
            text-align: center;
            color: white;
            padding: 4rem 0;
        }

        .hero h1 {
            font-size: 4rem;
            font-weight: 800;
            margin-bottom: 1rem;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
        }

        .hero .subtitle {
            font-size: 1.5rem;
            margin-bottom: 2rem;
            opacity: 0.9;
        }

        .hero .tagline {
            font-size: 1.125rem;
            max-width: 800px;
            margin: 0 auto;
            opacity: 0.8;
        }

        .section {
            background: white;
            border-radius: 1rem;
            padding: 3rem;
            margin: 2rem 0;
            box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1);
        }

        .section h2 {
            font-size: 2.5rem;
            color: #6366f1;
            margin-bottom: 2rem;
            text-align: center;
        }

        .grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
            gap: 2rem;
            margin-top: 2rem;
        }

        .card {
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 0.5rem;
            padding: 2rem;
            text-align: center;
            transition: transform 0.3s ease;
        }

        .card:hover {
            transform: translateY(-5px);
            box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.1);
        }

        .card-icon {
            font-size: 3rem;
            margin-bottom: 1rem;
        }

        .card h3 {
            color: #1e293b;
            margin-bottom: 1rem;
        }

        .code-block {
            background: #1e293b;
            color: #a3a3a3;
            padding: 1.5rem;
            border-radius: 0.5rem;
            font-family: 'Monaco', 'Consolas', monospace;
            font-size: 0.875rem;
            overflow-x: auto;
            margin: 1rem 0;
        }

        .stats {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 2rem;
            margin: 2rem 0;
        }

        .stat {
            text-align: center;
            color: white;
        }

        .stat-number {
            display: block;
            font-size: 3rem;
            font-weight: 700;
            color: #06b6d4;
        }

        .stat-label {
            font-size: 1rem;
            opacity: 0.8;
        }

        .cta {
            background: #1e293b;
            color: white;
            border-radius: 1rem;
            padding: 3rem;
            text-align: center;
            margin: 2rem 0;
        }

        .btn {
            display: inline-block;
            background: linear-gradient(135deg, #6366f1, #8b5cf6);
            color: white;
            padding: 1rem 2rem;
            border: none;
            border-radius: 0.5rem;
            text-decoration: none;
            font-weight: 600;
            margin: 0.5rem;
            transition: transform 0.3s ease;
        }

        .btn:hover {
            transform: translateY(-2px);
        }

        @media (max-width: 768px) {
            .hero h1 {
                font-size: 2.5rem;
            }
            
            .container {
                padding: 1rem;
            }
            
            .section {
                padding: 2rem;
            }
        }
    </style>
</head>
<body>
    <div class=""container"">
        <!-- Hero Section -->
        <div class=""hero"">
            <h1>TechDebtMaster</h1>
            <p class=""subtitle"">AI-Powered Technical Debt Management</p>
            <p class=""tagline"">Transform your codebase into a maintainable, high-quality asset with intelligent debt detection and human-centered solutions</p>
            
            <div class=""stats"">
                <div class=""stat"">
                    <span class=""stat-number"">85%</span>
                    <span class=""stat-label"">Debt Reduction</span>
                </div>
                <div class=""stat"">
                    <span class=""stat-number"">3x</span>
                    <span class=""stat-label"">Faster Fixes</span>
                </div>
                <div class=""stat"">
                    <span class=""stat-number"">100%</span>
                    <span class=""stat-label"">AI-Powered</span>
                </div>
            </div>
        </div>

        <!-- Problem Statement -->
        <div class=""section"">
            <h2>The Technical Debt Crisis</h2>
            <div class=""grid"">
                <div class=""card"">
                    <div class=""card-icon"">💰</div>
                    <h3>$85B Annual Cost</h3>
                    <p>The software industry spends billions annually on technical debt maintenance and remediation.</p>
                </div>
                <div class=""card"">
                    <div class=""card-icon"">⏰</div>
                    <h3>42% Developer Time</h3>
                    <p>Nearly half of development time is spent dealing with technical debt instead of building features.</p>
                </div>
                <div class=""card"">
                    <div class=""card-icon"">🚫</div>
                    <h3>67% Project Delays</h3>
                    <p>Most projects experience delays due to accumulated technical debt and maintainability issues.</p>
                </div>
            </div>
        </div>

        <!-- Solution -->
        <div class=""section"">
            <h2>AI-Powered Solution</h2>
            <div class=""grid"">
                <div class=""card"">
                    <div class=""card-icon"">🤖</div>
                    <h3>Intelligent Detection</h3>
                    <p>Advanced AI algorithms automatically identify technical debt patterns and maintainability issues.</p>
                </div>
                <div class=""card"">
                    <div class=""card-icon"">📊</div>
                    <h3>Smart Prioritization</h3>
                    <p>ML-driven severity assessment helps you focus on debt that matters most to your business.</p>
                </div>
                <div class=""card"">
                    <div class=""card-icon"">🛠️</div>
                    <h3>Actionable Insights</h3>
                    <p>Get specific recommendations with code examples and refactoring strategies.</p>
                </div>
            </div>
        </div>

        <!-- Demo -->
        <div class=""section"">
            <h2>See It In Action</h2>
            <div class=""grid"">
                <div>
                    <h3>🔍 Quick Analysis</h3>
                    <div class=""code-block"">
# Analyze your repository
tdm debt analyze ./src

# Get statistics  
tdm debt show --severity High

# Generate report
tdm debt report --open
                    </div>
                </div>
                <div>
                    <h3>🤖 AI Integration</h3>
                    <div class=""code-block"">
# Start MCP server
tdm mcp

# Use with AI assistants
""Analyze this code for technical debt
and provide refactoring suggestions""
                    </div>
                </div>
            </div>
        </div>

        <!-- Value Propositions -->
        <div class=""section"">
            <h2>Why TechDebtMaster?</h2>
            <div class=""grid"">
                <div class=""card"">
                    <div class=""card-icon"">🎯</div>
                    <h3>For Developers</h3>
                    <p>Focus on high-impact improvements, faster code reviews, and automated refactoring suggestions.</p>
                </div>
                <div class=""card"">
                    <div class=""card-icon"">🏢</div>
                    <h3>For Organizations</h3>
                    <p>Reduced maintenance costs, faster delivery, improved quality metrics, and strategic investment.</p>
                </div>
                <div class=""card"">
                    <div class=""card-icon"">🚀</div>
                    <h3>Enterprise Ready</h3>
                    <p>Security, compliance, and scalability built-in with enterprise-grade architecture.</p>
                </div>
            </div>
        </div>

        <!-- Call to Action -->
        <div class=""cta"">
            <h2>Ready to Transform Your Codebase?</h2>
            <p>Join the revolution in AI-powered technical debt management</p>
            <a href=""#"" class=""btn"">Start Free Trial</a>
            <a href=""#"" class=""btn"">Schedule Demo</a>
            <a href=""#"" class=""btn"">View Documentation</a>
        </div>
    </div>
</body>
</html>";

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
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

        if (!Directory.Exists(repositoryPath))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Repository path '{repositoryPath}' does not exist."
            );
            return 1;
        }

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var fileName = settings.OutputPath ?? $"tdm-walkthrough-{timestamp}.html";

            // Ensure the output path is absolute
            if (!Path.IsPathRooted(fileName))
            {
                fileName = Path.Combine(repositoryPath, fileName);
            }

            AnsiConsole.MarkupLine($"[green]Generating TechDebtMaster walkthrough...[/]");

            var htmlContent = HtmlTemplate;
            await File.WriteAllTextAsync(fileName, htmlContent);

            var fileInfo = new System.IO.FileInfo(fileName);
            var fileSizeKb = fileInfo.Length / 1024.0;

            AnsiConsole.MarkupLine($"[green]✓[/] Walkthrough generated successfully!");
            AnsiConsole.MarkupLine($"[dim]File:[/] {fileName}");
            AnsiConsole.MarkupLine($"[dim]Size:[/] {fileSizeKb:F1} KB");

            if (settings.Open)
            {
                AnsiConsole.MarkupLine("[dim]Opening in browser...[/]");
                OpenInBrowser(fileName);
            }

            return 0;
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to write file: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Access denied: {ex.Message}");
            return 1;
        }
    }

    private static void OpenInBrowser(string filePath)
    {
        try
        {
            var absolutePath = Path.GetFullPath(filePath);
            var url = $"file:///{absolutePath.Replace('\\', '/')}";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(
                    new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true }
                );
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not open browser: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning:[/] Access denied when opening browser: {ex.Message}"
            );
        }
    }

    public class Settings : CommandSettings
    {
        [Description("Path to the repository")]
        [CommandArgument(0, "[REPOSITORY_PATH]")]
        public string? RepositoryPath { get; init; }

        [Description("Output file path for the walkthrough HTML")]
        [CommandOption("-o|--output")]
        public string? OutputPath { get; init; }

        [Description("Open the generated walkthrough in the default browser")]
        [CommandOption("--open")]
        public bool Open { get; init; } = false;
    }
}
