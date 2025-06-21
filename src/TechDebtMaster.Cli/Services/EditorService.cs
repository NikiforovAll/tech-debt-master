using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TechDebtMaster.Cli.Services;

/// <summary>
/// Service for opening files in external editors
/// </summary>
public class EditorService : IEditorService
{
    public async Task OpenFileAsync(string filePath, string? editorCommand = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var editor = editorCommand ?? GetDefaultEditor();

        var processStartInfo = new ProcessStartInfo
        {
            FileName = editor,
            Arguments = $"\"{filePath}\"",
            UseShellExecute = true,
        };

        try
        {
            using var process = Process.Start(processStartInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to open file in editor '{editor}': {ex.Message}",
                ex
            );
        }
    }

    public string GetDefaultEditor()
    {
        // Check environment variables first
        var editor =
            Environment.GetEnvironmentVariable("EDITOR")
            ?? Environment.GetEnvironmentVariable("VISUAL");

        if (!string.IsNullOrEmpty(editor))
        {
            return editor;
        }

        // Platform-specific defaults
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "notepad";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "open";
        }
        else
        {
            // Linux/Unix - try common editors in order of preference
            var linuxEditors = new[] { "code", "nano", "vim", "vi" };

            foreach (var linuxEditor in linuxEditors)
            {
                if (IsCommandAvailable(linuxEditor))
                {
                    return linuxEditor;
                }
            }

            return "vi"; // Fallback - should be available on most Unix systems
        }
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(processStartInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
