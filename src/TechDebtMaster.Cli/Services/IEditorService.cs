namespace TechDebtMaster.Cli.Services;

/// <summary>
/// Service for opening files in external editors
/// </summary>
public interface IEditorService
{
    /// <summary>
    /// Opens a file in the default or specified editor
    /// </summary>
    /// <param name="filePath">Path to the file to open</param>
    /// <param name="editorCommand">Optional editor command to use instead of default</param>
    Task OpenFileAsync(string filePath, string? editorCommand = null);

    /// <summary>
    /// Gets the default editor command for the current platform
    /// </summary>
    /// <returns>Default editor command</returns>
    string GetDefaultEditor();
}