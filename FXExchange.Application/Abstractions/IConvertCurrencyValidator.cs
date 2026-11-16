
using FXExchange.Application.Models;

namespace FXExchange.Application.Abstractions;

public interface IConvertCurrencyValidator
{
    void Validate(
        CurrencyConversionRequest request,
        ExchangeRateSnapshot snapshot);
}
