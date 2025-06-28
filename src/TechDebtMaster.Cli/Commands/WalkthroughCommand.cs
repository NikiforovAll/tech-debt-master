using System.Diagnostics;
using System.Runtime.InteropServices;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

public class WalkthroughCommand(IWalkthroughService walkthroughService) : AsyncCommand<WalkthroughCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            // Get the default walkthrough path
            var defaultWalkthroughPath = await walkthroughService.GetDefaultWalkthroughPathAsync();

            if (!File.Exists(defaultWalkthroughPath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Default walkthrough not found at {defaultWalkthroughPath}");
                AnsiConsole.MarkupLine($"[dim]Try running 'tdm init' to ensure walkthrough is created.[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]Opening TechDebtMaster walkthrough...[/]");

            OpenInBrowser(defaultWalkthroughPath);

            return 0;
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to access walkthrough: {ex.Message}");
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
                    new ProcessStartInfo("cmd", $"/c start \"\" \"{url}\"")
                    {
                        CreateNoWindow = true,
                    }
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
    }

    public class Settings : CommandSettings
    {
        // No additional settings needed - just opens the default walkthrough
    }
}
