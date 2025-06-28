using System.Xml.Linq;
using Microsoft.SemanticKernel;

namespace TechDebtMaster.Cli.Services.Analysis.Handlers;

/// <summary>
/// Handler that analyzes file content for technical debt indicators using Semantic Kernel and Prompty
/// </summary>
public class TechDebtAnalysisHandler(
    Kernel kernel,
    ITechDebtStorageService techDebtStorage,
    ITemplateService templateService,
    IConfigurationService configurationService
) : IAnalysisHandler
{
    public const string ResultKey = "techdebt";

    public string HandlerName => "TechDebt";

    public async Task ProcessAsync(FileAnalysisContext context)
    {
        var xmlOutput = await AnalyzeTechnicalDebtAsync(context.FilePath, context.Content);

        if (!string.IsNullOrWhiteSpace(xmlOutput))
        {
            // Preprocess XML to extract debts section
            var preprocessedXml = PreprocessXmlContent(xmlOutput);

            if (!string.IsNullOrWhiteSpace(preprocessedXml))
            {
                // Parse XML and extract debt items
                var debtItems = await ParseXmlDebtItemsAsync(preprocessedXml, context.FilePath);

                if (debtItems.Count > 0)
                {
                    var analysisResult = new TechDebtAnalysisResult { Items = debtItems };
                    context.Results[ResultKey] = analysisResult;
                }
                else
                {
                    // No valid technical debt found
                    context.Results[ResultKey] = null!;
                }
            }
            else
            {
                // No debts section found
                context.Results[ResultKey] = null!;
            }
        }
        else
        {
            // No technical debt found
            context.Results[ResultKey] = null!;
        }
    }

    private async Task<string> AnalyzeTechnicalDebtAsync(string filePath, string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        // Get the default prompt template name from configuration
        var defaultPrompt =
            await configurationService.GetAsync("prompt.default") ?? "techdebt-analysis";
        var promptyPath = await templateService.GetTemplatePathAsync($"{defaultPrompt}.prompty");

#pragma warning disable SKEXP0040
        var function = kernel.CreateFunctionFromPromptyFile(promptyPath);
#pragma warning restore SKEXP0040

        var arguments = new KernelArguments
        {
            ["filePath"] = filePath,
            ["content"] = content,
            ["fileExtension"] = Path.GetExtension(filePath).TrimStart('.'),
        };

        var result = await function.InvokeAsync(kernel, arguments);
        return result.ToString() ?? string.Empty;
    }

    private static string PreprocessXmlContent(string xmlOutput)
    {
        if (string.IsNullOrWhiteSpace(xmlOutput))
        {
            return string.Empty;
        }

        // Find the opening and closing debts tags
        var startTag = "<debts>";
        var endTag = "</debts>";

        var startIndex = xmlOutput.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        if (startIndex == -1)
        {
            // No debts section found, return empty
            return string.Empty;
        }

        var endIndex = xmlOutput.IndexOf(endTag, startIndex, StringComparison.OrdinalIgnoreCase);
        if (endIndex == -1)
        {
            // No closing tag found, return empty
            return string.Empty;
        }

        // Extract the content including the debts tags
        var debtsSection = xmlOutput.Substring(startIndex, endIndex + endTag.Length - startIndex);

        return debtsSection.Trim();
    }

    private async Task<List<TechnicalDebtItem>> ParseXmlDebtItemsAsync(
        string xmlContent,
        string filePath
    )
    {
        var debtItems = new List<TechnicalDebtItem>();

        var doc = XDocument.Parse(xmlContent);
        var debtElements = doc.Descendants("debt");

        foreach (var debtElement in debtElements)
        {
            var content = debtElement.Element("content")?.Value ?? string.Empty;
            var id = debtElement.Attribute("id")?.Value ?? string.Empty;
            var summary = debtElement.Element("summary")?.Value ?? string.Empty;
            var tags = ParseTags(debtElement.Element("tags")?.Value);

            // Enhance content with metadata headers for user readability
            var enhancedContent = FormatContentWithMetadata(id, summary, filePath, tags, content);

            // Save each debt item's content to a separate file
            var reference = await techDebtStorage.SaveTechDebtAsync(enhancedContent, filePath);

            var debtItem = new TechnicalDebtItem
            {
                Id = id,
                Summary = summary,
                Severity = ParseSeverity(debtElement.Element("severity")?.Value),
                Tags = tags,
                Reference = reference,
            };

            debtItems.Add(debtItem);
        }

        return debtItems;
    }

    private static string FormatContentWithMetadata(
        string id,
        string summary,
        string filePath,
        DebtTag[] tags,
        string content
    )
    {
        var tagList = tags.Length > 0 
            ? string.Join(", ", tags.Select(t => t.ToString())) 
            : "";

        var frontmatter = $"""
            ---
            id: {id}
            summary: {summary}
            file: {filePath}
            tags: [{tagList}]
            ---

            """;

        return frontmatter + content.Trim();
    }

    private static DebtSeverity ParseSeverity(string? severityText)
    {
        if (string.IsNullOrWhiteSpace(severityText))
        {
            return DebtSeverity.Low;
        }

        if (Enum.TryParse<DebtSeverity>(severityText, ignoreCase: true, out var severity))
        {
            return severity;
        }

        return DebtSeverity.Low;
    }

    private static DebtTag[] ParseTags(string? tagsText)
    {
        if (string.IsNullOrWhiteSpace(tagsText))
        {
            return [];
        }

        return
        [
            .. tagsText
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrEmpty(tag))
                .Select(ParseSingleTag)
                .Where(tag => tag.HasValue)
                .Select(tag => tag!.Value),
        ];
    }

    private static DebtTag? ParseSingleTag(string tagText)
    {
        if (Enum.TryParse<DebtTag>(tagText, ignoreCase: true, out var tag))
        {
            return tag;
        }

        return null;
    }
}
