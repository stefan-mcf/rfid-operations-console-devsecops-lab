namespace RfidOps.Core.Domain;

public sealed record ReaderEvent(
    Guid EventId,
    string TagId,
    string ReaderId,
    DateTimeOffset OccurredAtUtc);
