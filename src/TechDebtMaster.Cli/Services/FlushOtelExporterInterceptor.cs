using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Spectre.Console.Cli;

namespace TechDebtMaster.Cli.Services;

public class FlushOtelExporterInterceptor : ICommandInterceptor
{
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        // This runs before command execution - no action needed
    }

    public void InterceptResult(CommandContext context, CommandSettings settings, ref int result)
    {
        // Flush and dispose telemetry after command execution
        ServiceConfiguration.TracerProvider.ForceFlush();
        ServiceConfiguration.TracerProvider.Dispose();
        ServiceConfiguration.MeterProvider.ForceFlush();
        ServiceConfiguration.MeterProvider.Dispose();
    }
}
