
using FXExchange.Application.Abstractions;
using FXExchange.Application.Constants;
using FXExchange.Application.Models;

namespace FXExchange.Application.Validation;

public sealed class ConvertCurrencyValidator
    : IConvertCurrencyValidator
{
    public void Validate(
        CurrencyConversionRequest request,
        ExchangeRateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (request.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Amount),
                 ApplicationConstants.NegativeAmount);
        }

         if (request.Amount == 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Amount),
                ApplicationConstants.ZeroAmount);
        }

        // Validate both currencies against the same snapshot.
        snapshot.GetRate(request.Pair.Source);
        snapshot.GetRate(request.Pair.Target);
    }
}
