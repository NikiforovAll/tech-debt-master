using System.Text.Json;

namespace TechDebtMaster.Cli.Services;

public interface IIndexStorageService
{
    Task<IndexData?> LoadLatestIndexAsync(string repositoryPath);
    Task SaveIndexAsync(string repositoryPath, IndexData indexData);
    string GetIndexDirectory(string repositoryPath);
}

public class IndexStorageService(IHashCalculator hashCalculator) : IIndexStorageService
{
    private const string IndexDirectoryName = ".tdm";
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<IndexData?> LoadLatestIndexAsync(string repositoryPath)
    {
        var repoHash = GetRepositoryHash(repositoryPath);
        var indexDir = GetIndexDirectory(repositoryPath);
        var metadataPath = Path.Combine(indexDir, $"metadata_{repoHash}.json");

        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            var metadataJson = await File.ReadAllTextAsync(metadataPath);
            var metadata = JsonSerializer.Deserialize<IndexMetadata>(metadataJson, _jsonOptions);

            if (metadata?.LatestIndexFile == null)
            {
                return null;
            }

            var indexPath = Path.Combine(indexDir, metadata.LatestIndexFile);
            if (!File.Exists(indexPath))
            {
                return null;
            }

            var indexJson = await File.ReadAllTextAsync(indexPath);
            return JsonSerializer.Deserialize<IndexData>(indexJson, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveIndexAsync(string repositoryPath, IndexData indexData)
    {
        var repoHash = GetRepositoryHash(repositoryPath);
        var indexDir = GetIndexDirectory(repositoryPath);
        Directory.CreateDirectory(indexDir);

        var indexFileName = $"index_{repoHash}_{indexData.Timestamp:yyyyMMdd_HHmmss}.json";
        var indexPath = Path.Combine(indexDir, indexFileName);

        var indexJson = JsonSerializer.Serialize(indexData, _jsonOptions);
        await File.WriteAllTextAsync(indexPath, indexJson);

        var metadata = new IndexMetadata
        {
            RepositoryPath = repositoryPath,
            LatestIndexFile = indexFileName,
            LastUpdated = indexData.Timestamp,
        };

        var metadataPath = Path.Combine(indexDir, $"metadata_{repoHash}.json");
        var metadataJson = JsonSerializer.Serialize(metadata, _jsonOptions);
        await File.WriteAllTextAsync(metadataPath, metadataJson);
    }

    public string GetIndexDirectory(string repositoryPath)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), IndexDirectoryName);
    }

    private string GetRepositoryHash(string repositoryPath)
    {
        var normalizedPath = Path.GetFullPath(repositoryPath).ToLowerInvariant();
        var hash = hashCalculator.CalculateHash(normalizedPath);
        return hash.Substring(0, 8);
    }
}

public class IndexData
{
    public DateTime Timestamp { get; set; }
    public string RepositoryPath { get; set; } = string.Empty;
    public Dictionary<string, FileInfo> Files { get; set; } = new();
    public IndexSummary Summary { get; set; } = new();
}

public class FileInfo
{
    public string Hash { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
}

public class IndexSummary
{
    public int TotalFiles { get; set; }
    public List<string> ChangedFiles { get; set; } = new();
    public List<string> NewFiles { get; set; } = new();
    public List<string> DeletedFiles { get; set; } = new();
    public bool HasChanges =>
        ChangedFiles.Count != 0 || NewFiles.Count != 0 || DeletedFiles.Count != 0;
}

public class IndexMetadata
{
    public string RepositoryPath { get; set; } = string.Empty;
    public string LatestIndexFile { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}
