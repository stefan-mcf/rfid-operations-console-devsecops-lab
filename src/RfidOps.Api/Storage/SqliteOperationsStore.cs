using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RfidOps.Core.Domain;

namespace RfidOps.Api.Storage;

public sealed class SqliteOperationsStore
{
    private readonly string connectionString;

    public SqliteOperationsStore(IConfiguration configuration)
    {
        var configuredPath = configuration["RFID_OPS_DB_PATH"] ?? "data/rfid-ops.db";
        var databasePath = Path.GetFullPath(configuredPath);
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS tag_registrations (
                tag_id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                state TEXT NOT NULL CHECK (state IN ('Active', 'Inactive')),
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS access_events (
                event_id TEXT PRIMARY KEY,
                tag_id TEXT NOT NULL,
                reader_id TEXT NOT NULL,
                occurred_at_utc TEXT NOT NULL,
                outcome TEXT NOT NULL CHECK (outcome IN ('Granted', 'Denied')),
                reason TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sync_outbox (
                event_id TEXT PRIMARY KEY REFERENCES access_events(event_id),
                payload_json TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                acknowledged_at_utc TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_access_events_occurred_at
                ON access_events(occurred_at_utc DESC);
            CREATE INDEX IF NOT EXISTS ix_sync_outbox_pending
                ON sync_outbox(acknowledged_at_utc);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            return Convert.ToInt64(
                await command.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture) == 1;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    public async Task UpsertTagAsync(
        TagRegistration registration,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tag_registrations(tag_id, display_name, state, updated_at_utc)
            VALUES ($tagId, $displayName, $state, $updatedAtUtc)
            ON CONFLICT(tag_id) DO UPDATE SET
                display_name = excluded.display_name,
                state = excluded.state,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$tagId", registration.TagId);
        command.Parameters.AddWithValue("$displayName", registration.DisplayName);
        command.Parameters.AddWithValue("$state", registration.State.ToString());
        command.Parameters.AddWithValue("$updatedAtUtc", registration.UpdatedAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TagRegistration?> GetTagAsync(
        string tagId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tag_id, display_name, state, updated_at_utc
            FROM tag_registrations
            WHERE tag_id = $tagId;
            """;
        command.Parameters.AddWithValue("$tagId", tagId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadTag(reader)
            : null;
    }

    public async Task<IReadOnlyList<TagRegistration>> ListTagsAsync(
        CancellationToken cancellationToken = default)
    {
        var registrations = new List<TagRegistration>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tag_id, display_name, state, updated_at_utc
            FROM tag_registrations
            ORDER BY tag_id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            registrations.Add(ReadTag(reader));
        }

        return registrations;
    }

    public async Task RecordDecisionAsync(
        AccessDecision decision,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(
            cancellationToken);

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                INSERT INTO access_events(
                    event_id, tag_id, reader_id, occurred_at_utc, outcome, reason)
                VALUES ($eventId, $tagId, $readerId, $occurredAtUtc, $outcome, $reason);
                """;
            eventCommand.Parameters.AddWithValue("$eventId", decision.EventId.ToString("D"));
            eventCommand.Parameters.AddWithValue("$tagId", decision.TagId);
            eventCommand.Parameters.AddWithValue("$readerId", decision.ReaderId);
            eventCommand.Parameters.AddWithValue("$occurredAtUtc", decision.OccurredAtUtc.ToString("O"));
            eventCommand.Parameters.AddWithValue("$outcome", decision.Outcome.ToString());
            eventCommand.Parameters.AddWithValue("$reason", decision.Reason.ToString());
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var outboxCommand = connection.CreateCommand())
        {
            outboxCommand.Transaction = transaction;
            outboxCommand.CommandText = """
                INSERT INTO sync_outbox(event_id, payload_json, created_at_utc)
                VALUES ($eventId, $payloadJson, $createdAtUtc);
                """;
            outboxCommand.Parameters.AddWithValue("$eventId", decision.EventId.ToString("D"));
            outboxCommand.Parameters.AddWithValue("$payloadJson", JsonSerializer.Serialize(decision));
            outboxCommand.Parameters.AddWithValue("$createdAtUtc", DateTimeOffset.UtcNow.ToString("O"));
            await outboxCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> AcknowledgeOutboxAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE sync_outbox
            SET acknowledged_at_utc = $acknowledgedAtUtc
            WHERE event_id = $eventId AND acknowledged_at_utc IS NULL;
            """;
        command.Parameters.AddWithValue("$eventId", eventId.ToString("D"));
        command.Parameters.AddWithValue("$acknowledgedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<IReadOnlyList<PersistedDecision>> ListRecentDecisionsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var decisions = new List<PersistedDecision>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.event_id, e.tag_id, e.reader_id, e.occurred_at_utc,
                   e.outcome, e.reason,
                   CASE WHEN o.acknowledged_at_utc IS NULL THEN 1 ELSE 0 END AS pending
            FROM access_events e
            JOIN sync_outbox o ON o.event_id = e.event_id
            ORDER BY e.occurred_at_utc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            decisions.Add(new PersistedDecision(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                Enum.Parse<AccessOutcome>(reader.GetString(4)),
                Enum.Parse<AccessReason>(reader.GetString(5)),
                reader.GetInt64(6) == 1));
        }

        return decisions;
    }

    public async Task<OperationsStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM tag_registrations),
                (SELECT COUNT(*) FROM access_events),
                (SELECT COUNT(*) FROM access_events WHERE outcome = 'Granted'),
                (SELECT COUNT(*) FROM access_events WHERE outcome = 'Denied'),
                (SELECT COUNT(*) FROM sync_outbox WHERE acknowledged_at_utc IS NULL);
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new OperationsStatus(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt64(4),
            DateTimeOffset.UtcNow);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static TagRegistration ReadTag(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            Enum.Parse<TagState>(reader.GetString(2)),
            DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture));
}
