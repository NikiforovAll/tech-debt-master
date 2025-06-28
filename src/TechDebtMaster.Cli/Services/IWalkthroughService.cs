namespace TechDebtMaster.Cli.Services;

/// <summary>
/// Interface for managing walkthrough files
/// </summary>
public interface IWalkthroughService
{
    /// <summary>
    /// Ensures the default walkthrough HTML file exists in the user directory
    /// </summary>
    Task EnsureDefaultWalkthroughAsync();

    /// <summary>
    /// Gets the walkthroughs directory path
    /// </summary>
    string GetWalkthroughsDirectory();

    /// <summary>
    /// Gets the path to the default walkthrough HTML file
    /// </summary>
    Task<string> GetDefaultWalkthroughPathAsync();
}
