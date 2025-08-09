var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder
    .AddOllama("ollama")
    .WithImageTag("0.6.0")
    .WithOpenWebUI(ui => ui.WithImageTag("0.5.20"))
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
var r1 = ollama.AddModel("deepseek-r1", "deepseek-r1:1.5b");

// ollama.AddModel("gpt-oss", "gpt-oss:20b");

builder
    .AddProject<Projects.TechDebtMaster_Cli>("tdm")
    .WithArgs("--", "help")
    .WithReference(r1);

builder.Build().Run();
