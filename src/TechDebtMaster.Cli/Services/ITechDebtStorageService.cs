using TechDebtMaster.Cli.Services.Analysis;

namespace TechDebtMaster.Cli.Services;

/// <summary>
/// Service for storing and retrieving technical debt analysis results in separate files
/// </summary>
public interface ITechDebtStorageService
{
    /// <summary>
    /// Saves technical debt markdown analysis to a separate file with MD5-based naming
    /// </summary>
    /// <param name="markdownContent">The markdown content from AI analysis</param>
    /// <param name="filePath">The original file path that was analyzed</param>
    /// <returns>A reference to the saved techdebt file</returns>
    Task<TechDebtReference> SaveTechDebtAsync(string markdownContent, string filePath);

    /// <summary>
    /// Loads technical debt markdown content from a separate file
    /// </summary>
    /// <param name="reference">The reference to the techdebt file</param>
    /// <returns>The markdown content, or empty string if not found</returns>
    Task<string> LoadTechDebtAsync(TechDebtReference reference);

    /// <summary>
    /// Checks if a techdebt file exists for the given reference
    /// </summary>
    /// <param name="reference">The reference to check</param>
    /// <returns>True if the file exists, false otherwise</returns>
    Task<bool> ExistsAsync(TechDebtReference reference);

    /// <summary>
    /// Deletes a techdebt file
    /// </summary>
    /// <param name="reference">The reference to the file to delete</param>
    /// <returns>True if the file was deleted, false if it didn't exist</returns>
    Task<bool> DeleteAsync(TechDebtReference reference);
}
