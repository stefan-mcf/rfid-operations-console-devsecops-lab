using RfidOps.Core.Domain;

namespace RfidOps.Api.Storage;

public sealed record OperationsStatus(
    long RegisteredTags,
    long AccessEvents,
    long GrantedEvents,
    long DeniedEvents,
    long PendingOutbox,
    DateTimeOffset ObservedAtUtc);

public sealed record PersistedDecision(
    Guid EventId,
    string TagId,
    string ReaderId,
    DateTimeOffset OccurredAtUtc,
    AccessOutcome Outcome,
    AccessReason Reason,
    bool PendingDelivery);
