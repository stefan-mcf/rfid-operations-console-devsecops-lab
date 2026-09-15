namespace RfidOps.Core.Domain;

public enum AccessOutcome
{
    Granted,
    Denied,
}

public enum AccessReason
{
    ActiveRegistration,
    InactiveRegistration,
    UnknownTag,
}

public sealed record AccessDecision(
    Guid EventId,
    string TagId,
    string ReaderId,
    DateTimeOffset OccurredAtUtc,
    AccessOutcome Outcome,
    AccessReason Reason);
