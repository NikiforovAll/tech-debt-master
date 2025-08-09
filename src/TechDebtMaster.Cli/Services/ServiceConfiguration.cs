using System.Diagnostics;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TechDebtMaster.Cli.Commands;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Services.Analysis.Handlers;

namespace TechDebtMaster.Cli.Services;

public static class ServiceConfiguration
{
    public static readonly ActivitySource ActivitySource = new("TechDebtMaster");

    // OTEL_EXPORTER_OTLP_ENDPOINT=""
    // OTEL_EXPORTER_OTLP_PROTOCOL="grpc"
    // OTEL_EXPORTER_OTLP_HEADERS=""
    public static readonly TracerProvider TracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("tdm"))
        .AddSource("TechDebtMaster")
        .AddSource("Microsoft.SemanticKernel*")
        .AddHttpClientInstrumentation()
        .AddOtlpExporter()
        .Build();

    public static readonly MeterProvider MeterProvider = Sdk.CreateMeterProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("tdm"))
        .AddMeter("Microsoft.SemanticKernel*")
        .AddOtlpExporter()
        .Build();

    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        services.AddScoped<Kernel>(provider =>
        {
            var configService = provider.GetRequiredService<IConfigurationService>();

            var config = configService.GetConfiguration();
            var builder = Kernel.CreateBuilder();

            // Configure based on provider
            if (config.Provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
            {
                builder.AddOpenAIChatCompletion(modelId: config.Model, apiKey: config.ApiKey);
            }
            else if (config.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
            {
                builder.AddOllamaChatCompletion(
                    modelId: config.Model,
                    endpoint: new Uri(config.BaseUrl)
                );
            }
            else if (config.Provider.Equals("dial", StringComparison.OrdinalIgnoreCase))
            {
                var httpClient = new HttpClient(
                    new HttpClientHandler { CheckCertificateRevocationList = false }
                );

                // Default to DIAL (Azure OpenAI compatible)
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: config.Model,
                    endpoint: config.BaseUrl,
                    apiKey: config.ApiKey,
                    serviceId: null,
                    modelId: config.Model,
                    httpClient: httpClient
                );
            }
            else
            {
                throw new NotSupportedException($"Provider '{config.Provider}' is not supported.");
            }

            return builder.Build();
        });
        services.AddScoped<DefaultCommand>();
        services.AddScoped<AnalyzeIndexCommand>();
        services.AddScoped<AnalyzeStatusCommand>();
        services.AddScoped<AnalyzeDebtCommand>();
        services.AddScoped<DebtReportCommand>();
        services.AddScoped<DebtImportCommand>();
        services.AddScoped<CleanCommand>();
        services.AddScoped<ConfigShowCommand>();
        services.AddScoped<ConfigSetCommand>();
        services.AddScoped<DialLimitsCommand>();
        services.AddScoped<McpServerCommand>();
        services.AddScoped<PromptsEditCommand>();
        services.AddScoped<PromptsRestoreTemplatesCommand>();
        services.AddScoped<PromptsSetDefaultCommand>();
        services.AddScoped<IRepositoryIndexService, RepositoryIndexService>();
        services.AddScoped<IIndexStorageService, IndexStorageService>();
        services.AddScoped<IHashCalculator, HashCalculator>();
        services.AddScoped<IChangeDetector, ChangeDetector>();
        services.AddScoped<IRepomixParser, RepomixParser>();
        services.AddScoped<IProcessRunner, ProcessRunner>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<IDialService, DialService>();
        services.AddHttpClient<IDialService, DialService>();
        services.AddScoped<ITechDebtStorageService, TechDebtStorageService>();
        services.AddSingleton<ITemplateService, TemplateService>();
        services.AddScoped<IEditorService, EditorService>();
        services.AddScoped<IHtmlReportGenerator, HtmlReportGenerator>();
        services.AddScoped<IReportStateExtractor, ReportStateExtractor>();

        // Register analysis handlers
        services.AddScoped<IAnalysisHandler, PreviewHandler>();
        services.AddScoped<IAnalysisHandler, TechDebtAnalysisHandler>();

        return services;
    }
}
