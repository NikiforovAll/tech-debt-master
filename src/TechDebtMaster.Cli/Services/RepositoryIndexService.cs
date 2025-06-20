namespace TechDebtMaster.Cli.Services;

public interface IRepositoryIndexService
{
    Task<IndexResult> IndexRepositoryAsync(string repositoryPath);
    string GetLastIndexedContent();
}

public class IndexResult
{
    public string FileSummary { get; set; } = string.Empty;
    public IndexSummary ChangeSummary { get; set; } = new();
    public bool HasChanges => ChangeSummary.ChangedFiles.Any() || 
                              ChangeSummary.NewFiles.Any() || 
                              ChangeSummary.DeletedFiles.Any();
}

public class RepositoryIndexService : IRepositoryIndexService
{
    private readonly IIndexStorageService _storageService;
    private readonly IHashCalculator _hashCalculator;
    private readonly IChangeDetector _changeDetector;
    private readonly IRepomixParser _repomixParser;
    private string _lastIndexedContent = string.Empty;

    public RepositoryIndexService(
        IIndexStorageService storageService,
        IHashCalculator hashCalculator,
        IChangeDetector changeDetector,
        IRepomixParser repomixParser)
    {
        _storageService = storageService;
        _hashCalculator = hashCalculator;
        _changeDetector = changeDetector;
        _repomixParser = repomixParser;
    }

    public async Task<IndexResult> IndexRepositoryAsync(string repositoryPath)
    {
        var repomixOutput = await RunRepomixAsync(repositoryPath);
        var parsedData = _repomixParser.ParseXmlOutput(repomixOutput);
        
        _lastIndexedContent = parsedData.FileSummary;

        var previousIndex = await _storageService.LoadLatestIndexAsync(repositoryPath);
        
        var currentIndex = new IndexData
        {
            Timestamp = DateTime.UtcNow,
            RepositoryPath = repositoryPath,
            Files = new Dictionary<string, FileInfo>()
        };

        foreach (var (path, fileData) in parsedData.Files)
        {
            currentIndex.Files[path] = new FileInfo
            {
                Hash = _hashCalculator.CalculateHash(fileData.Content),
                Size = fileData.Content.Length,
                LastModified = DateTime.UtcNow
            };
        }

        var changeSummary = _changeDetector.DetectChanges(previousIndex, currentIndex);
        currentIndex.Summary = changeSummary;

        await _storageService.SaveIndexAsync(repositoryPath, currentIndex);

        return new IndexResult
        {
            FileSummary = parsedData.FileSummary,
            ChangeSummary = changeSummary
        };
    }

    public string GetLastIndexedContent()
    {
        return _lastIndexedContent;
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

}
