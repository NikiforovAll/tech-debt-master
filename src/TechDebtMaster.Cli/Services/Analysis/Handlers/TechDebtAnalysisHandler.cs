using System.Xml.Linq;
using Microsoft.SemanticKernel;

namespace TechDebtMaster.Cli.Services.Analysis.Handlers;

/// <summary>
/// Handler that analyzes file content for technical debt indicators using Semantic Kernel and Prompty
/// </summary>
public class TechDebtAnalysisHandler(
    Kernel kernel,
    ITechDebtStorageService techDebtStorage,
    ITemplateService templateService
) : IAnalysisHandler
{
    public const string ResultKey = "techdebt";

    public string HandlerName => "TechDebt";

    public async Task ProcessAsync(FileAnalysisContext context)
    {
        var xmlOutput = await AnalyzeTechnicalDebtAsync(context.FilePath, context.Content);

        if (!string.IsNullOrWhiteSpace(xmlOutput))
        {
            // Parse XML and extract debt items
            var debtItems = await ParseXmlDebtItemsAsync(xmlOutput, context.FilePath);

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

        var promptyPath = await templateService.GetTemplatePathAsync("techdebt-analysis.prompty");

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

            // Save each debt item's content to a separate file
            var reference = await techDebtStorage.SaveTechDebtAsync(content, filePath);

            var debtItem = new TechnicalDebtItem
            {
                Id = debtElement.Attribute("id")?.Value ?? string.Empty,
                Summary = debtElement.Element("summary")?.Value ?? string.Empty,
                Severity = ParseSeverity(debtElement.Element("severity")?.Value),
                Tags = ParseTags(debtElement.Element("tags")?.Value),
                Reference = reference,
            };

            debtItems.Add(debtItem);
        }

        return debtItems;
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
        return tagText
            .ToLowerInvariant()
            .Replace("-", "", StringComparison.OrdinalIgnoreCase) switch
        {
            "codesmell" => DebtTag.CodeSmell,
            "naming" => DebtTag.Naming,
            "magicnumber" => DebtTag.MagicNumber,
            "complexity" => DebtTag.Complexity,
            "errorhandling" => DebtTag.ErrorHandling,
            "outdatedpattern" => DebtTag.OutdatedPattern,
            "todo" => DebtTag.Todo,
            "performance" => DebtTag.Performance,
            _ => null,
        };
    }
}
