namespace Ticket.Domain.Support;

public static class SearchNormalizer
{
    public static string NormalizeRequired(string? input)
        => string.IsNullOrWhiteSpace(input) ? string.Empty : input.Trim().ToUpperInvariant();

    public static string? NormalizeOptional(string? input)
        => string.IsNullOrWhiteSpace(input) ? null : input.Trim().ToUpperInvariant();
}
