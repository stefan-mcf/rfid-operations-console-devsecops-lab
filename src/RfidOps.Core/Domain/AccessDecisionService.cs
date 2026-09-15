namespace RfidOps.Core.Domain;

public static class AccessDecisionService
{
    public static AccessDecision Evaluate(ReaderEvent readerEvent, TagRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(readerEvent);

        if (registration is null)
        {
            return Create(readerEvent, AccessOutcome.Denied, AccessReason.UnknownTag);
        }

        return registration.State switch
        {
            TagState.Active => Create(
                readerEvent,
                AccessOutcome.Granted,
                AccessReason.ActiveRegistration),
            TagState.Inactive => Create(
                readerEvent,
                AccessOutcome.Denied,
                AccessReason.InactiveRegistration),
            _ => throw new ArgumentOutOfRangeException(
                nameof(registration),
                registration.State,
                "Unsupported tag state."),
        };
    }

    private static AccessDecision Create(
        ReaderEvent readerEvent,
        AccessOutcome outcome,
        AccessReason reason) =>
        new(
            readerEvent.EventId,
            readerEvent.TagId,
            readerEvent.ReaderId,
            readerEvent.OccurredAtUtc,
            outcome,
            reason);
}
