namespace TechDebtMaster.Cli.Services;

/// <summary>
/// Service for managing template files, including copying from assembly to user directory
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Gets the path to a template file, copying it to the user directory if needed
    /// </summary>
    /// <param name="templateName">Name of the template file (e.g., "techdebt-analysis.prompty")</param>
    /// <returns>Path to the template file in the user directory</returns>
    Task<string> GetTemplatePathAsync(string templateName);

    /// <summary>
    /// Ensures all templates are copied to the user directory
    /// </summary>
    Task EnsureTemplatesAsync();

    /// <summary>
    /// Gets the templates directory path in the user's .techdebtmaster folder
    /// </summary>
    string GetTemplatesDirectory();

    /// <summary>
    /// Gets a list of available template names (without .prompty extension)
    /// </summary>
    /// <returns>List of template names available for editing</returns>
    Task<IEnumerable<string>> GetAvailableTemplatesAsync();

    /// <summary>
    /// Forces restoration of all templates from assembly, overriding existing ones
    /// </summary>
    Task ForceRestoreTemplatesAsync();
}