using FXExchange.Application.Models;

namespace FXExchange.Application.Abstractions
{
    public interface IExchangeRateProvider
    {
        ValueTask<ExchangeRateSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
    }
}
