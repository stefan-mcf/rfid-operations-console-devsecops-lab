namespace RfidOps.Core.Domain;

public sealed record TagRegistration(
    string TagId,
    string DisplayName,
    TagState State,
    DateTimeOffset UpdatedAtUtc);
