namespace TechDebtMaster.Cli.Services.Analysis;

/// <summary>
/// Interface for analysis handlers that process file content and generate analysis results
/// </summary>
public interface IAnalysisHandler
{
    /// <summary>
    /// Unique name for this handler
    /// </summary>
    string HandlerName { get; }

    /// <summary>
    /// Process the file analysis context and add results
    /// </summary>
    /// <param name="context">The analysis context containing file information and results</param>
    Task ProcessAsync(FileAnalysisContext context);
}
