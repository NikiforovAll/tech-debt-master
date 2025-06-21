using Microsoft.SemanticKernel;

namespace TechDebtMaster.Cli.Services.Analysis.Handlers;

/// <summary>
/// Handler that analyzes file content for technical debt indicators using Semantic Kernel and Prompty
/// </summary>
public class TechDebtAnalysisHandler(Kernel kernel, ITechDebtStorageService techDebtStorage, ITemplateService templateService)
    : IAnalysisHandler
{
    private readonly Kernel _kernel = kernel;
    private readonly ITechDebtStorageService _techDebtStorage = techDebtStorage;
    private readonly ITemplateService _templateService = templateService;
    public const string ResultKey = "techdebt";

    public string HandlerName => "TechDebt";

    public async Task ProcessAsync(FileAnalysisContext context)
    {
        var markdownOutput = await AnalyzeTechnicalDebtAsync(context.FilePath, context.Content);

        if (!string.IsNullOrWhiteSpace(markdownOutput))
        {
            // Save markdown analysis to separate file and create analysis result
            var reference = await _techDebtStorage.SaveTechDebtAsync(
                markdownOutput,
                context.FilePath
            );
            var analysisResult = new TechDebtAnalysisResult
            {
                Reference = reference,
                Severity = DebtSeverity.Low,
            };
            context.Results[ResultKey] = analysisResult;
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
}
