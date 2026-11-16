
using FXExchange.Console.Constants;
using FXExchange.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace FXExchange.Console.ExceptionHandling;

public sealed class ConsoleExceptionHandler
{
    private readonly ILogger<ConsoleExceptionHandler> _logger;

    public ConsoleExceptionHandler(
        ILogger<ConsoleExceptionHandler> logger)
    {
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public int Handle(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        switch (exception)
        {
            case UnknownCurrencyException:
            case ArgumentException:
            case OverflowException:
                _logger.LogWarning(
                    "FX request rejected. Type={Type}, Reason={Reason}",
                    exception.GetType().Name,
                    exception.Message);

                WriteError(exception.Message);

                return ConsoleConstants.ValidationExitCode;

            default:
                _logger.LogError(
                    exception,
                    "Unexpected FX conversion failure");

                WriteError(ConsoleConstants.UnexpectedError);

                return ConsoleConstants.ErrorExitCode;
        }
    }

    private static void WriteError(string message)
    {
        ConsoleColor originalColor =
            System.Console.ForegroundColor;

        try
        {
            System.Console.ForegroundColor = ConsoleColor.Red;

            System.Console.Error.WriteLine(
                $"Error: {message}");
        }
        finally
        {
            System.Console.ForegroundColor = originalColor;
        }
    }
}
