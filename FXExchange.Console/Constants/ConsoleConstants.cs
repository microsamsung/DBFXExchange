
namespace FXExchange.Console.Constants;

public static class ConsoleConstants
{
    public const string InteractiveArgument = "--interactive";
    public const string ExitCommand = "exit";

    public const string Usage =
        "Usage: FXExchange.Console EUR/DKK 100";

    public const string Welcome =
        "FX Exchange interactive mode. Enter EUR/DKK 100 or exit.";

    public const string InvalidAmount =
        "Invalid amount. Use '.' as decimal separator.";

    public const string UnexpectedError =
        "An unexpected error occurred. Check application logs.";

    public const string OutputFormat =
        "0.############################";

    public const int SuccessExitCode = 0;
    public const int ErrorExitCode = 1;
    public const int ValidationExitCode = 2;
}
