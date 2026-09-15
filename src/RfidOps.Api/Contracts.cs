using RfidOps.Core.Domain;

namespace RfidOps.Api;

public sealed record UpsertTagRequest(string? DisplayName, TagState State);

public sealed record CreateReaderEventRequest(
    string TagId,
    string ReaderId,
    DateTimeOffset? OccurredAtUtc);
