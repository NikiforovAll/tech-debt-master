using System.Reflection;

namespace TechDebtMaster.Cli.Services;

/// <summary>
/// Service for managing template files, including copying from assembly to user directory
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly string _templatesDirectory;
    private readonly string _assemblyTemplatesDirectory;

    public TemplateService()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var techDebtMasterDirectory = Path.Combine(homeDirectory, ".techdebtmaster");
        _templatesDirectory = Path.Combine(techDebtMasterDirectory, "templates");

        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        _assemblyTemplatesDirectory = Path.Combine(assemblyDir, "Templates");
    }

    public async Task<string> GetTemplatePathAsync(string templateName)
    {
        await EnsureTemplatesAsync();

        var templatePath = Path.Combine(_templatesDirectory, templateName);

        if (File.Exists(templatePath))
        {
            return templatePath;
        }

        throw new FileNotFoundException(
            $"Template file '{templateName}' not found in templates directory: {_templatesDirectory}"
        );
    }

    public async Task EnsureTemplatesAsync()
    {
        // Create templates directory if it doesn't exist
        if (!Directory.Exists(_templatesDirectory))
        {
            Directory.CreateDirectory(_templatesDirectory);
        }

        // Copy templates from assembly location to user directory
        if (Directory.Exists(_assemblyTemplatesDirectory))
        {
            await CopyTemplatesFromAssemblyAsync();
        }
    }

    public string GetTemplatesDirectory()
    {
        return _templatesDirectory;
    }

    public async Task<IEnumerable<string>> GetAvailableTemplatesAsync()
    {
        await EnsureTemplatesAsync();

        if (!Directory.Exists(_templatesDirectory))
        {
            return [];
        }

        var templateFiles = Directory.GetFiles(
            _templatesDirectory,
            "*.prompty",
            SearchOption.AllDirectories
        );

        return templateFiles
            .Select(filePath => Path.GetRelativePath(_templatesDirectory, filePath))
            .Select(relativePath => Path.GetFileNameWithoutExtension(relativePath))
            .OrderBy(name => name);
    }

    public async Task ForceRestoreTemplatesAsync()
    {
        // Create templates directory if it doesn't exist
        if (!Directory.Exists(_templatesDirectory))
        {
            Directory.CreateDirectory(_templatesDirectory);
        }

        // Force copy templates from assembly location to user directory, overriding existing ones
        if (Directory.Exists(_assemblyTemplatesDirectory))
        {
            await ForceCopyTemplatesFromAssemblyAsync();
        }
    }

    private async Task CopyTemplatesFromAssemblyAsync()
    {
        var assemblyTemplateFiles = Directory.GetFiles(
            _assemblyTemplatesDirectory,
            "*",
            SearchOption.AllDirectories
        );

        foreach (var sourceFile in assemblyTemplateFiles)
        {
            var relativePath = Path.GetRelativePath(_assemblyTemplatesDirectory, sourceFile);
            var destinationFile = Path.Combine(_templatesDirectory, relativePath);

            // Create subdirectory if needed
            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (
                !string.IsNullOrEmpty(destinationDirectory)
                && !Directory.Exists(destinationDirectory)
            )
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Copy file if it doesn't exist or if source is newer
            if (
                !File.Exists(destinationFile)
                || File.GetLastWriteTime(sourceFile) > File.GetLastWriteTime(destinationFile)
            )
            {
                await Task.Run(() => File.Copy(sourceFile, destinationFile, overwrite: true));
            }
        }
    }

    private async Task ForceCopyTemplatesFromAssemblyAsync()
    {
        var assemblyTemplateFiles = Directory.GetFiles(
            _assemblyTemplatesDirectory,
            "*",
            SearchOption.AllDirectories
        );

        foreach (var sourceFile in assemblyTemplateFiles)
        {
            var relativePath = Path.GetRelativePath(_assemblyTemplatesDirectory, sourceFile);
            var destinationFile = Path.Combine(_templatesDirectory, relativePath);

            // Create subdirectory if needed
            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (
                !string.IsNullOrEmpty(destinationDirectory)
                && !Directory.Exists(destinationDirectory)
            )
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Always copy and overwrite existing files
            await Task.Run(() => File.Copy(sourceFile, destinationFile, overwrite: true));
        }
    }
}
