using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Commands;
using TechDebtMaster.Cli.Services;

var services = new ServiceCollection();
services.ConfigureServices();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config
        .AddCommand<AnalyzeCommand>("analyze")
        .WithDescription("Analyze a repository for technical debt")
        .WithExample("analyze", "/home/user/my-repo");
});

return await app.RunAsync(args);
