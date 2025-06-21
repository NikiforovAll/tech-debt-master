using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using TechDebtMaster.Cli.Commands;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Services.Analysis.Handlers;

namespace TechDebtMaster.Cli.Services;

public static class ServiceConfiguration
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        services.AddScoped<Kernel>(provider =>
        {
            var httpClient = new HttpClient(
                new HttpClientHandler { CheckCertificateRevocationList = false }
            );

            var configService = provider.GetRequiredService<IConfigurationService>();

            var config = configService.GetConfiguration();
            var builder = Kernel
                .CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: config.Model,
                    endpoint: config.BaseUrl,
                    apiKey: config.ApiKey,
                    serviceId: null,
                    modelId: config.Model,
                    httpClient: httpClient
                );

            return builder.Build();
        });
        services.AddScoped<DefaultCommand>();
        services.AddScoped<AnalyzeIndexCommand>();
        services.AddScoped<AnalyzeStatusCommand>();
        services.AddScoped<AnalyzeDebtCommand>();
        services.AddScoped<CleanCommand>();
        services.AddScoped<ConfigShowCommand>();
        services.AddScoped<ConfigSetCommand>();
        services.AddScoped<DialListModelsCommand>();
        services.AddScoped<DialLimitsCommand>();
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

        // Register analysis handlers
        services.AddScoped<IAnalysisHandler, PreviewHandler>();
        services.AddScoped<IAnalysisHandler, TechDebtAnalysisHandler>();

        return services;
    }
}
