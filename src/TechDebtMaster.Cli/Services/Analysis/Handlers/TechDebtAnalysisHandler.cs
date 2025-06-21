using System.Text.Json;
using Microsoft.SemanticKernel;

namespace TechDebtMaster.Cli.Services.Analysis.Handlers;

/// <summary>
/// Handler that analyzes file content for technical debt indicators using Semantic Kernel and Prompty
/// </summary>
public class TechDebtAnalysisHandler : IAnalysisHandler
{
    private readonly Kernel _kernel;
    public const string ResultKey = "techdebt";
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public TechDebtAnalysisHandler(Kernel kernel)
    {
        _kernel = kernel;
    }

    public string HandlerName => "TechDebt";

    public async Task ProcessAsync(FileAnalysisContext context)
    {
        var technicalDebtItems = await AnalyzeTechnicalDebtAsync(context.FilePath, context.Content);
        context.Results[ResultKey] = technicalDebtItems;
    }

    private async Task<TechnicalDebtItem[]> AnalyzeTechnicalDebtAsync(
        string filePath,
        string content
    )
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var assemblyDir = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location
        )!;
        var promptyPath = Path.Combine(assemblyDir, "Templates", "techdebt-analysis.prompty");

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
        var jsonResponse = result.ToString();

        if (string.IsNullOrWhiteSpace(jsonResponse))
        {
            return [];
        }

        return [new() { Summary = jsonResponse, Severity = DebtSeverity.Low }];

        // var debtData = JsonSerializer.Deserialize<TechnicalDebtResponse[]>(
        //     jsonResponse,
        //     s_jsonOptions
        // );

        // return debtData
        //         ?.Select(d => new TechnicalDebtItem
        //         {
        //             Summary = d.Summary,
        //             Severity = Enum.TryParse<DebtSeverity>(d.Severity, true, out var severity)
        //                 ? severity
        //                 : DebtSeverity.Low,
        //         })
        //         .ToArray() ?? [];
    }

    private class TechnicalDebtResponse
    {
        public string Summary { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }
}
