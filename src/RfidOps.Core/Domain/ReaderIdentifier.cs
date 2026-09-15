using System.Text.RegularExpressions;

namespace RfidOps.Core.Domain;

public static partial class ReaderIdentifier
{
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().ToUpperInvariant();
        if (!ValidReaderId().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Reader identifiers must contain 3-64 uppercase letters, numbers or hyphens.",
                nameof(value));
        }

        return normalized;
    }

    [GeneratedRegex("^[A-Z0-9](?:[A-Z0-9-]{1,62})[A-Z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidReaderId();
}
