using System.Text.RegularExpressions;

namespace RfidOps.Core.Domain;

public static partial class TagIdentifier
{
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().ToUpperInvariant();
        if (!ValidTagId().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Tag identifiers must contain 3-64 uppercase letters, numbers or hyphens.",
                nameof(value));
        }

        return normalized;
    }

    [GeneratedRegex("^[A-Z0-9](?:[A-Z0-9-]{1,62})[A-Z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidTagId();
}
