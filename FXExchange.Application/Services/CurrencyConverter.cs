
using FXExchange.Application.Abstractions;
using FXExchange.Application.Models;
using Microsoft.Extensions.Logging;

namespace FXExchange.Application.Services;

public sealed class CurrencyConverter : ICurrencyConverter
{
    private readonly IExchangeRateProvider _rateProvider;
    private readonly IConvertCurrencyValidator _validator;
    private readonly IConversionStrategyFactory _strategyFactory;
    private readonly ILogger<CurrencyConverter> _logger;

    public CurrencyConverter(
        IExchangeRateProvider rateProvider,
        IConvertCurrencyValidator validator,
        IConversionStrategyFactory strategyFactory,
        ILogger<CurrencyConverter> logger)
    {
        _rateProvider = rateProvider
            ?? throw new ArgumentNullException(nameof(rateProvider));

        _validator = validator
            ?? throw new ArgumentNullException(nameof(validator));

        _strategyFactory = strategyFactory
            ?? throw new ArgumentNullException(nameof(strategyFactory));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<decimal> ConvertAsync(
        CurrencyConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = await _rateProvider.GetSnapshotAsync(
            cancellationToken);

        _validator.Validate(request, snapshot);

        var strategy = _strategyFactory.Create(request.Pair);

        decimal result = strategy.Convert(
            request.Pair,
            request.Amount,
            snapshot);

        _logger.LogInformation(
            "Converted {Amount} {Source} to {Result} {Target}. Rate version: {Version}",
            request.Amount,
            request.Pair.Source,
            result,
            request.Pair.Target,
            snapshot.Version);

        return result;
    }
}
