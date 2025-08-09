using System.Diagnostics;
using Spectre.Console.Cli;

namespace TechDebtMaster.Cli.Services;

public class OpenTelemetryCommandInterceptor : ICommandInterceptor
{
    private Activity? _activity;

    public void Intercept(CommandContext context, CommandSettings settings)
    {
        this._activity = ServiceConfiguration.ActivitySource.StartActivity(context.Name);
        _activity?.SetTag("command.name", context.Name);
    }

    public void InterceptResult(CommandContext context, CommandSettings settings, ref int result)
    {
        _activity?.SetTag("command.result", result);
        _activity?.Stop();
    }
}
