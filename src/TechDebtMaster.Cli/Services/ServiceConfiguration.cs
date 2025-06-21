using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using TechDebtMaster.Cli.Commands;

namespace TechDebtMaster.Cli.Services;

public static class ServiceConfiguration
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        var handler = new HttpClientHandler();
        handler.CheckCertificateRevocationList = false;
        var httpClient = new HttpClient(handler);

        services.AddScoped<Kernel>(provider =>
        {
            var configService = provider.GetRequiredService<IConfigurationService>();

            var endpoint =
                configService.GetAsync("ai.endpoint").Result ?? "https://ai-proxy.lab.epam.com/";
            var apiKey =
                configService.GetAsync("ai.key").Result
                ?? Environment.GetEnvironmentVariable("DIAL_API_KEY")
                ?? throw new InvalidOperationException(
                    "AI API key not configured. Use 'config set ai.key <key>' or set DIAL_API_KEY environment variable."
                );
            var deploymentName = configService.GetAsync("ai.model").Result ?? "gpt-4o-mini-2024-07-18";

            var builder = Kernel
                .CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: deploymentName,
                    endpoint: endpoint,
                    apiKey: apiKey,
                    serviceId: null,
                    modelId: deploymentName,
                    httpClient: httpClient
                );

            return builder.Build();
        });
        services.AddScoped<DefaultCommand>();
        services.AddScoped<AnalyzeCommand>();
        services.AddScoped<CleanCommand>();
        services.AddScoped<ConfigShowCommand>();
        services.AddScoped<ConfigSetCommand>();
        services.AddScoped<IRepositoryIndexService, RepositoryIndexService>();
        services.AddScoped<IIndexStorageService, IndexStorageService>();
        services.AddScoped<IHashCalculator, HashCalculator>();
        services.AddScoped<IChangeDetector, ChangeDetector>();
        services.AddScoped<IRepomixParser, RepomixParser>();
        services.AddScoped<IAnalysisService, AnalysisService>();

        return services;
    }
}
