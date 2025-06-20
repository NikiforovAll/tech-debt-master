using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using TechDebtMaster.Cli.Commands;

namespace TechDebtMaster.Cli.Services;

public static class ServiceConfiguration
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        var handler = new HttpClientHandler();
        handler.CheckCertificateRevocationList = false;
        var httpClient = new HttpClient(handler);

        var endpoint = "https://ai-proxy.lab.epam.com/";
        var apiKey =
            Environment.GetEnvironmentVariable("DIAL_API_KEY")
            ?? throw new InvalidOperationException(
                "Environment variable 'DIAL_API_KEY' is not set. Please set it to your OpenAI API key."
            );
        var deploymentName = "gpt-35-turbo";
        var apiVersion = "2023-08-01-preview";

        services.AddScoped<Kernel>(provider =>
        {
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
        services.AddScoped<AnalyzeCommand>();
        services.AddScoped<IRepositoryIndexService, RepositoryIndexService>();
        services.AddScoped<IIndexStorageService, IndexStorageService>();
        services.AddScoped<IHashCalculator, HashCalculator>();
        services.AddScoped<IChangeDetector, ChangeDetector>();
        services.AddScoped<IRepomixParser, RepomixParser>();
        services.AddScoped<IAnalysisService, AnalysisService>();

        return services;
    }
}
