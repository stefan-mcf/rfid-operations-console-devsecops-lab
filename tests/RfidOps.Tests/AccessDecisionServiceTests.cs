using RfidOps.Core.Domain;

namespace RfidOps.Tests;

public sealed class AccessDecisionServiceTests
{
    private readonly ReaderEvent readerEvent = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        "DEMO-TAG-001",
        "DEMO-READER-01",
        DateTimeOffset.Parse("2026-08-13T00:00:00Z", CultureInfo.InvariantCulture));

    [Fact]
    public void ActiveRegistrationIsGranted()
    {
        var registration = new TagRegistration(
            readerEvent.TagId,
            "Demo vehicle",
            TagState.Active,
            readerEvent.OccurredAtUtc);

        var result = AccessDecisionService.Evaluate(readerEvent, registration);

        Assert.Equal(AccessOutcome.Granted, result.Outcome);
        Assert.Equal(AccessReason.ActiveRegistration, result.Reason);
    }

    [Fact]
    public void InactiveRegistrationIsDenied()
    {
        var registration = new TagRegistration(
            readerEvent.TagId,
            "Demo vehicle",
            TagState.Inactive,
            readerEvent.OccurredAtUtc);

        var result = AccessDecisionService.Evaluate(readerEvent, registration);

        Assert.Equal(AccessOutcome.Denied, result.Outcome);
        Assert.Equal(AccessReason.InactiveRegistration, result.Reason);
    }

    [Fact]
    public void UnknownTagIsDenied()
    {
        var result = AccessDecisionService.Evaluate(readerEvent, null);

        Assert.Equal(AccessOutcome.Denied, result.Outcome);
        Assert.Equal(AccessReason.UnknownTag, result.Reason);
    }
}
