namespace TechDebtMaster.Cli.Services;

public interface IProcessRunner
{
    Task<string> RunRepomixAsync(string repositoryPath);
}