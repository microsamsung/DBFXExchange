
using System.Globalization;
using FXExchange.Application.Abstractions;
using FXExchange.Application.Models;
using FXExchange.Console.Constants;
using FXExchange.Console.ExceptionHandling;
using FXExchange.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FXExchange.Console.Services;

public sealed class ConsoleRunner : IConsoleRunner
{
    private readonly ICurrencyConverter _converter;
    private readonly ConsoleExceptionHandler _exceptionHandler;
    private readonly ILogger<ConsoleRunner> _logger;

    public ConsoleRunner(
        ICurrencyConverter converter,
        ConsoleExceptionHandler exceptionHandler,
        ILogger<ConsoleRunner> logger)
    {
        _converter = converter
            ?? throw new ArgumentNullException(nameof(converter));

        _exceptionHandler = exceptionHandler
            ?? throw new ArgumentNullException(nameof(exceptionHandler));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 ||
            (args.Length == 1 &&
             string.Equals(
                 args[0],
                 ConsoleConstants.InteractiveArgument,
                 StringComparison.OrdinalIgnoreCase)))
        {
            return await RunInteractiveAsync(cancellationToken);
        }

        return await ExecuteAsync(args, cancellationToken);
    }

    private async Task<int> RunInteractiveAsync(
        CancellationToken cancellationToken)
    {
        WriteHeader();

        while (!cancellationToken.IsCancellationRequested)
        {
            WritePrompt("Enter Currency Pair (EUR/DKK): ");

            string? pairInput = System.Console.ReadLine();

            if (ShouldExit(pairInput))
                break;

            if (string.IsNullOrWhiteSpace(pairInput))
            {
                WriteError("Currency pair is required.");
                continue;
            }

            WritePrompt("Enter Amount: ");

            string? amountInput = System.Console.ReadLine();

            if (ShouldExit(amountInput))
                break;

            await ExecuteAsync(
                new[]
                {
                    pairInput.Trim(),
                    amountInput ?? string.Empty
                },
                cancellationToken);

            System.Console.WriteLine();
        }

        _logger.LogInformation(
            "FX converter interactive session ended.");

        return cancellationToken.IsCancellationRequested
            ? ConsoleConstants.ErrorExitCode
            : ConsoleConstants.SuccessExitCode;
    }

    private async Task<int> ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (args.Length != 2)
            {
                throw new ArgumentException(
                    ConsoleConstants.Usage);
            }

            CurrencyPair pair = CurrencyPair.Parse(args[0]);

            if (!decimal.TryParse(
                args[1],
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal amount))
            {
                throw new ArgumentException(
                    ConsoleConstants.InvalidAmount);
            }

            var request = new CurrencyConversionRequest(
                pair,
                amount);

            decimal result = await _converter.ConvertAsync(
                request,
                cancellationToken);

            ShowResult(pair, amount, result);

            return ConsoleConstants.SuccessExitCode;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Currency conversion cancelled.");

            return ConsoleConstants.ErrorExitCode;
        }
        catch (Exception ex)
        {
            return _exceptionHandler.Handle(ex);
        }
    }

    private static bool ShouldExit(string? input)
    {
        return input is null ||
            string.Equals(
                input.Trim(),
                ConsoleConstants.ExitCommand,
                StringComparison.OrdinalIgnoreCase);
    }

    private static void WritePrompt(string message)
    {
        ConsoleColor originalColor =
            System.Console.ForegroundColor;

        try
        {
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.Write(message);
        }
        finally
        {
            System.Console.ForegroundColor = originalColor;
        }
    }

    private static void WriteHeader()
    {
        ConsoleColor originalColor =
            System.Console.ForegroundColor;

        try
        {
            //System.Console.WriteLine(ConsoleConstants.Welcome);
            //System.Console.WriteLine();
            System.Console.ForegroundColor = ConsoleColor.Cyan;

            System.Console.WriteLine(
                "========================================");

            System.Console.WriteLine(
                "          FX CURRENCY CONVERTER");

            System.Console.WriteLine(
                "========================================");
        }
        finally
        {
            System.Console.ForegroundColor = originalColor;
        }

        System.Console.WriteLine(
            "Supported: DKK, EUR, USD, GBP, SEK, NOK, CHF, JPY");

        System.Console.WriteLine(
            "Type 'exit' at any prompt to quit.");

        System.Console.WriteLine();
    }

    private static void ShowResult(
        CurrencyPair pair,
        decimal amount,
        decimal result)
    {
        string formattedAmount = amount.ToString(
            ConsoleConstants.OutputFormat,
            CultureInfo.InvariantCulture);

        string formattedResult = result.ToString(
            ConsoleConstants.OutputFormat,
            CultureInfo.InvariantCulture);

        ConsoleColor originalForeground =
            System.Console.ForegroundColor;

        ConsoleColor originalBackground =
            System.Console.BackgroundColor;

        try
        {
            System.Console.WriteLine();

            System.Console.ForegroundColor = ConsoleColor.Black;
            System.Console.BackgroundColor = ConsoleColor.Green;

            System.Console.WriteLine(
                $"  SUCCESS: {formattedAmount} {pair.Source} " +
                $"= {formattedResult} {pair.Target}  ");
        }
        finally
        {
            System.Console.ForegroundColor = originalForeground;
            System.Console.BackgroundColor = originalBackground;
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
