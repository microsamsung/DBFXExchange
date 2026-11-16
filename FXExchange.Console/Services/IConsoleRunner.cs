
namespace FXExchange.Console.Services;

public interface IConsoleRunner
{
    Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken = default);
}
