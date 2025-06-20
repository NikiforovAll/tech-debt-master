namespace TechDebtMaster.Cli.Services;

public interface IChangeDetector
{
    IndexSummary DetectChanges(IndexData? previousIndex, IndexData currentIndex);
}

public class ChangeDetector : IChangeDetector
{
    public IndexSummary DetectChanges(IndexData? previousIndex, IndexData currentIndex)
    {
        var summary = new IndexSummary
        {
            TotalFiles = currentIndex.Files.Count
        };

        if (previousIndex == null)
        {
            summary.NewFiles = currentIndex.Files.Keys.ToList();
            return summary;
        }

        var previousFiles = previousIndex.Files;
        var currentFiles = currentIndex.Files;

        foreach (var (path, fileInfo) in currentFiles)
        {
            if (!previousFiles.ContainsKey(path))
            {
                summary.NewFiles.Add(path);
            }
            else if (previousFiles[path].Hash != fileInfo.Hash)
            {
                summary.ChangedFiles.Add(path);
            }
        }

        foreach (var path in previousFiles.Keys)
        {
            if (!currentFiles.ContainsKey(path))
            {
                summary.DeletedFiles.Add(path);
            }
        }

        return summary;
    }
}