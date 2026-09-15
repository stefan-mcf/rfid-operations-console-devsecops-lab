using Microsoft.Extensions.Configuration;
using RfidOps.Api.Storage;
using RfidOps.Core.Domain;

namespace RfidOps.Tests;

public sealed class SqliteOperationsStoreTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        $"rfid-ops-tests-{Guid.NewGuid():N}");
    private SqliteOperationsStore store = null!;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(directory);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RFID_OPS_DB_PATH"] = Path.Combine(directory, "test.db"),
            })
            .Build();
        store = new SqliteOperationsStore(configuration);
        await store.InitializeAsync();
    }

    public ValueTask DisposeAsync()
    {
        Directory.Delete(directory, recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RegistrationCanBeUpsertedAndRead()
    {
        var registration = new TagRegistration(
            "DEMO-TAG-001",
            "Demo vehicle",
            TagState.Active,
            DateTimeOffset.Parse("2026-08-13T00:00:00Z", CultureInfo.InvariantCulture));

        var cancellationToken = TestContext.Current.CancellationToken;
        await store.UpsertTagAsync(registration, cancellationToken);
        var result = await store.GetTagAsync(registration.TagId, cancellationToken);

        Assert.Equal(registration, result);
    }

    [Fact]
    public async Task DecisionAndOutboxAreCommittedTogether()
    {
        var decision = new AccessDecision(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "UNKNOWN-TAG-01",
            "DEMO-READER-01",
            DateTimeOffset.Parse("2026-08-13T00:00:00Z", CultureInfo.InvariantCulture),
            AccessOutcome.Denied,
            AccessReason.UnknownTag);

        var cancellationToken = TestContext.Current.CancellationToken;
        await store.RecordDecisionAsync(decision, cancellationToken);
        var status = await store.GetStatusAsync(cancellationToken);

        Assert.Equal(1, status.AccessEvents);
        Assert.Equal(1, status.DeniedEvents);
        Assert.Equal(1, status.PendingOutbox);

        Assert.True(await store.AcknowledgeOutboxAsync(decision.EventId, cancellationToken));
        Assert.False(await store.AcknowledgeOutboxAsync(decision.EventId, cancellationToken));
        Assert.Equal(0, (await store.GetStatusAsync(cancellationToken)).PendingOutbox);
    }
}
