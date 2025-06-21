namespace TechDebtMaster.Cli.Services.Analysis.Handlers;

/// <summary>
/// Handler that generates a preview/summary of file content
/// </summary>
public class PreviewHandler : IAnalysisHandler
{
    public const string ResultKey = "preview";

    public string HandlerName => "Preview";

    public Task ProcessAsync(FileAnalysisContext context)
    {
        var preview = GeneratePreview(context.Content);
        context.Results[ResultKey] = preview;

        return Task.CompletedTask;
    }

    private static string GeneratePreview(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var preview = content.Length > 100 ? content[..100] + "..." : content;

        // Replace newlines with spaces for better display
        return preview.Replace('\n', ' ').Replace('\r', ' ');
    }
}
