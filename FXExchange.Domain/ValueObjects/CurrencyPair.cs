
using System.Text.RegularExpressions;

namespace FXExchange.Domain.ValueObjects;

public sealed record CurrencyPair
{
    private const char DefaultSeparator = '/';

    private static readonly Regex PairPattern = new(
        $@"^\s*([A-Za-z]{{3}})\s*{Regex.Escape(DefaultSeparator.ToString())}\s*([A-Za-z]{{3}})\s*$",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public string Source { get; }

    public string Target { get; }

    private CurrencyPair(string source, string target)
    {
        Source = source;
        Target = target;
    }

    public static CurrencyPair Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException(
                "Currency pair is required.",
                nameof(input));
        }

        Match match = PairPattern.Match(input);

        if (!match.Success)
        {
            throw new ArgumentException(
                "Invalid currency pair. Expected EUR/DKK.",
                nameof(input));
        }

        return new CurrencyPair(
            match.Groups[1].Value.ToUpperInvariant(),
            match.Groups[2].Value.ToUpperInvariant());
    }

    public override string ToString()
    {
        return $"{Source}{DefaultSeparator}{Target}";
    }
}
