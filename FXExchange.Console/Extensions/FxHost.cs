
using FXExchange.Console.Constants;
using FXExchange.Console.ExceptionHandling;
using FXExchange.Console.Services;
using FXExchange.Infrastructure;
using FXExchange.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FXExchange.Console.Extensions;

public static class FxHost
{
    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.Configure<ExchangeRateOptions>(
                        context.Configuration.GetSection(
                            ExchangeRateOptions.SectionName));

                    services.Configure<RateSimulationOptions>(
                        context.Configuration.GetSection(
                            RateSimulationOptions.SectionName));

                    services.AddFxExchange();

                    services.AddSingleton<ConsoleExceptionHandler>();

                    services.AddSingleton<
                        IConsoleRunner,
                        ConsoleRunner>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole(options =>
                    {
                        options.LogToStandardErrorThreshold =
                            LogLevel.Trace;
                    });
                })
                .Build();

            await host.StartAsync(cancellationToken);

            try
            {
                var runner = host.Services
                    .GetRequiredService<IConsoleRunner>();

                return await runner.RunAsync(
                    args,
                    cancellationToken);
            }
            finally
            {
                await host.StopAsync(CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return ConsoleConstants.ErrorExitCode;
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine(
                $"Application startup/shutdown failed: {ex.Message}");

            return ConsoleConstants.ErrorExitCode;
        }
    }
}
