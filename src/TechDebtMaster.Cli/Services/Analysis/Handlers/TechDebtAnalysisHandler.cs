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
    private readonly Kernel _kernel = kernel;
    private readonly ITechDebtStorageService _techDebtStorage = techDebtStorage;
    private readonly ITemplateService _templateService = templateService;
    public const string ResultKey = "techdebt";

    public string HandlerName => "TechDebt";

    public async Task ProcessAsync(FileAnalysisContext context)
    {
        var xmlOutput = await AnalyzeTechnicalDebtAsync(context.FilePath, context.Content);

        if (!string.IsNullOrWhiteSpace(xmlOutput))
        {
            // Parse XML and extract debt items
            var debtItems = ParseXmlDebtItems(xmlOutput);

            if (debtItems.Count > 0)
            {
                // Save XML analysis to separate file and create analysis result
                var reference = await _techDebtStorage.SaveTechDebtAsync(
                    xmlOutput,
                    context.FilePath
                );
                var analysisResult = new TechDebtAnalysisResult
                {
                    Reference = reference,
                    Items = debtItems,
                };
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

        var promptyPath = await _templateService.GetTemplatePathAsync("techdebt-analysis.prompty");

#pragma warning disable SKEXP0040
        var function = _kernel.CreateFunctionFromPromptyFile(promptyPath);
#pragma warning restore SKEXP0040

        var arguments = new KernelArguments
        {
            ["filePath"] = filePath,
            ["content"] = content,
            ["fileExtension"] = Path.GetExtension(filePath).TrimStart('.'),
        };

        var result = await function.InvokeAsync(_kernel, arguments);
        return result.ToString() ?? string.Empty;
    }

    private static List<TechnicalDebtItem> ParseXmlDebtItems(string xmlContent)
    {
        var debtItems = new List<TechnicalDebtItem>();

        var doc = XDocument.Parse(xmlContent);
        var debtElements = doc.Descendants("debt");

        foreach (var debtElement in debtElements)
        {
            var debtItem = new TechnicalDebtItem
            {
                Id = debtElement.Attribute("id")?.Value ?? string.Empty,
                Summary = debtElement.Element("summary")?.Value ?? string.Empty,
                Severity = ParseSeverity(debtElement.Element("severity")?.Value),
                Tags = debtElement.Element("tags")?.Value ?? string.Empty,
                Content = debtElement.Element("content")?.Value ?? string.Empty,
            };

            debtItems.Add(debtItem);
        }

        return debtItems;
    }

    private static DebtSeverity ParseSeverity(string? severityText)
    {
        return severityText?.ToLowerInvariant() switch
        {
            "low" => DebtSeverity.Low,
            "medium" => DebtSeverity.Medium,
            "high" => DebtSeverity.High,
            "critical" => DebtSeverity.Critical,
            _ => DebtSeverity.Low,
        };
    }
}
