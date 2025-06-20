namespace TechDebtMaster.Cli.Services;

public interface IRepositoryIndexService
{
    Task<string> IndexRepositoryAsync(string repositoryPath);
    string GetLastIndexedContent();
}

public class RepositoryIndexService : IRepositoryIndexService
{
    private string _lastIndexedContent = string.Empty;

    public async Task<string> IndexRepositoryAsync(string repositoryPath)
    {
        var fileSummary = await RunRepomixAndParseAsync(repositoryPath);
        _lastIndexedContent = fileSummary;
        return fileSummary;
    }

    public string GetLastIndexedContent()
    {
        return _lastIndexedContent;
    }

    private static async Task<string> RunRepomixAndParseAsync(string repositoryPath)
    {
        var repomixOutput = await RunRepomixAsync(repositoryPath);
        return ParseFileSummary(repomixOutput);
    }

    private static async Task<string> RunRepomixAsync(string repositoryPath)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "repomix.cmd";
            process.StartInfo.Arguments = $"--stdout --style xml \"{repositoryPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = errorBuilder.ToString();
                throw new InvalidOperationException($"Repomix failed with error: {error}");
            }

            return outputBuilder.ToString();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to run repomix. Make sure it's installed and in PATH: {ex.Message}",
                ex
            );
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Unexpected error running repomix: {ex.Message}",
                ex
            );
        }
    }

    private static string ParseFileSummary(string repomixOutput)
    {
        const string FileSummaryTag = "<file_summary>";

        var index = repomixOutput.IndexOf(FileSummaryTag, StringComparison.OrdinalIgnoreCase);

        return index == -1 ? string.Empty : repomixOutput[index..];
    }
}
