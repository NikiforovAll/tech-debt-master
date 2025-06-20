using System.Text.RegularExpressions;

namespace TechDebtMaster.Cli.Services;

public interface IRepomixParser
{
    RepomixData ParseXmlOutput(string xmlContent);
}

public class RepomixParser : IRepomixParser
{
    public RepomixData ParseXmlOutput(string xmlContent)
    {
        var result = new RepomixData();
        
        try
        {
            // Use regex to find all file entries
            var filePattern = @"<file\s+path=""([^""]+)""[^>]*>(.*?)</file>";
            var fileMatches = Regex.Matches(xmlContent, filePattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            
            foreach (Match match in fileMatches)
            {
                if (match.Groups.Count >= 3)
                {
                    var path = match.Groups[1].Value;
                    var content = match.Groups[2].Value;
                    
                    // Decode XML entities if present
                    content = System.Net.WebUtility.HtmlDecode(content);
                    
                    result.Files[path] = new RepomixFileData
                    {
                        Path = path,
                        Content = content
                    };
                }
            }

            // Extract file_summary section
            var summaryPattern = @"<file_summary>(.*?)</file_summary>";
            var summaryMatch = Regex.Match(xmlContent, summaryPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            
            if (summaryMatch.Success && summaryMatch.Groups.Count >= 2)
            {
                result.FileSummary = summaryMatch.Groups[0].Value; // Include the tags
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse repomix output: {ex.Message}", ex);
        }

        return result;
    }
}

public class RepomixData
{
    public Dictionary<string, RepomixFileData> Files { get; set; } = new();
    public string FileSummary { get; set; } = string.Empty;
}

public class RepomixFileData
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}