
using FXExchange.Application.Abstractions;
using FXExchange.Application.Services;
using FXExchange.Application.Validation;
using FXExchange.Infrastructure.BackgroundServices;
using FXExchange.Infrastructure.Factories;
using FXExchange.Infrastructure.Providers;
using FXExchange.Infrastructure.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace FXExchange.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFxExchange(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ConcurrentExchangeRateProvider>();

        services.AddSingleton<IExchangeRateProvider>(
            sp => sp.GetRequiredService<
                ConcurrentExchangeRateProvider>());

        services.AddSingleton<IExchangeRateUpdater>(
            sp => sp.GetRequiredService<
                ConcurrentExchangeRateProvider>());

        services.AddSingleton<
            IConvertCurrencyValidator,
            ConvertCurrencyValidator>();

        services.AddSingleton<
            IConversionStrategy,
            IdentityConversionStrategy>();

        services.AddSingleton<
            IConversionStrategy,
            CrossRateConversionStrategy>();

        services.AddSingleton<
            IConversionStrategyFactory,
            ConversionStrategyFactory>();

        services.AddSingleton<
            ICurrencyConverter,
            CurrencyConverter>();

        services.AddHostedService<ExchangeRateBackgroundService>();

        return services;
    }
}
