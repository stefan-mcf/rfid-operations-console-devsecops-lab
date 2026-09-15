using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using RfidOps.Api.Security;
using RfidOps.Api.Storage;
using RfidOps.Core.Domain;

namespace RfidOps.Api;

internal static class EndpointMappings
{
    public static void Map(WebApplication app, IConfiguration configuration)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapGet("/favicon.ico", () => Results.NoContent());
        app.MapGet("/health", GetHealthAsync);
        app.MapGet("/metrics", GetMetricsAsync);

        var api = app.MapGroup("/api/v1");
        api.MapGet("/status", GetStatusAsync);
        api.MapGet("/tags", ListTagsAsync);
        api.MapGet("/events", ListEventsAsync);
        api.MapPut("/tags/{tagId}", UpsertTagAsync)
            .AddEndpointFilter(new ApiKeyEndpointFilter(
                "X-Admin-Key",
                "RFID_OPS_ADMIN_KEY",
                configuration));
        api.MapPost("/events", CreateEventAsync)
            .AddEndpointFilter(new ApiKeyEndpointFilter(
                "X-Reader-Key",
                "RFID_OPS_READER_KEY",
                configuration));
        api.MapPost("/outbox/{eventId:guid}/acknowledge", AcknowledgeOutboxAsync)
            .AddEndpointFilter(new ApiKeyEndpointFilter(
                "X-Admin-Key",
                "RFID_OPS_ADMIN_KEY",
                configuration));

        app.MapFallbackToFile("index.html");
    }

    private static async Task<IResult> GetHealthAsync(
        SqliteOperationsStore operationsStore,
        CancellationToken cancellationToken)
    {
        var databaseReachable = await operationsStore.PingAsync(cancellationToken);
        return Results.Json(
            new
            {
                status = databaseReachable ? "healthy" : "degraded",
                service = "rfid-operations-console",
                database = databaseReachable ? "reachable" : "unreachable",
                observedAtUtc = DateTimeOffset.UtcNow,
            },
            statusCode: databaseReachable
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> GetMetricsAsync(
        SqliteOperationsStore operationsStore,
        CancellationToken cancellationToken)
    {
        var status = await operationsStore.GetStatusAsync(cancellationToken);
        var metrics = new StringBuilder()
            .AppendLine("# HELP rfid_ops_up Whether the API and local store are reachable.")
            .AppendLine("# TYPE rfid_ops_up gauge")
            .AppendLine("rfid_ops_up 1")
            .AppendLine("# HELP rfid_ops_registered_tags Current number of synthetic tag registrations.")
            .AppendLine("# TYPE rfid_ops_registered_tags gauge")
            .AppendLine("rfid_ops_registered_tags " + status.RegisteredTags.ToString(CultureInfo.InvariantCulture))
            .AppendLine("# HELP rfid_ops_access_events_total Total synthetic access events processed.")
            .AppendLine("# TYPE rfid_ops_access_events_total counter")
            .AppendLine("rfid_ops_access_events_total " + status.AccessEvents.ToString(CultureInfo.InvariantCulture))
            .AppendLine("# HELP rfid_ops_access_decisions_total Synthetic decisions by outcome.")
            .AppendLine("# TYPE rfid_ops_access_decisions_total counter")
            .AppendLine("rfid_ops_access_decisions_total{outcome=\"granted\"} " + status.GrantedEvents.ToString(CultureInfo.InvariantCulture))
            .AppendLine("rfid_ops_access_decisions_total{outcome=\"denied\"} " + status.DeniedEvents.ToString(CultureInfo.InvariantCulture))
            .AppendLine("# HELP rfid_ops_outbox_pending Current events awaiting downstream acknowledgement.")
            .AppendLine("# TYPE rfid_ops_outbox_pending gauge")
            .AppendLine("rfid_ops_outbox_pending " + status.PendingOutbox.ToString(CultureInfo.InvariantCulture))
            .ToString();

        return Results.Text(metrics, "text/plain; version=0.0.4; charset=utf-8");
    }

    private static async Task<IResult> GetStatusAsync(
        SqliteOperationsStore operationsStore,
        CancellationToken cancellationToken) =>
        Results.Ok(await operationsStore.GetStatusAsync(cancellationToken));

    private static async Task<IResult> ListTagsAsync(
        SqliteOperationsStore operationsStore,
        CancellationToken cancellationToken) =>
        Results.Ok(await operationsStore.ListTagsAsync(cancellationToken));

    private static async Task<IResult> ListEventsAsync(
        int? limit,
        SqliteOperationsStore operationsStore,
        CancellationToken cancellationToken) =>
        Results.Ok(await operationsStore.ListRecentDecisionsAsync(limit ?? 20, cancellationToken));

    private static async Task<IResult> UpsertTagAsync(
        string tagId,
        UpsertTagRequest request,
        SqliteOperationsStore operationsStore,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalizedTagId = TagIdentifier.Normalize(tagId);
            var displayName = request.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 80)
            {
                return Results.BadRequest(new { error = "DisplayName must contain 1-80 characters." });
            }

            var registration = new TagRegistration(
                normalizedTagId,
                displayName,
                request.State,
                DateTimeOffset.UtcNow);
            await operationsStore.UpsertTagAsync(registration, cancellationToken);
            return Results.Ok(registration);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> CreateEventAsync(
        CreateReaderEventRequest request,
        SqliteOperationsStore operationsStore,
        CancellationToken cancellationToken)
    {
        try
        {
            var tagId = TagIdentifier.Normalize(request.TagId);
            var readerId = ReaderIdentifier.Normalize(request.ReaderId);
            var occurredAtUtc = request.OccurredAtUtc ?? DateTimeOffset.UtcNow;

            if (occurredAtUtc > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return Results.BadRequest(new { error = "OccurredAtUtc cannot be more than five minutes in the future." });
            }

            var readerEvent = new ReaderEvent(Guid.NewGuid(), tagId, readerId, occurredAtUtc);
            var registration = await operationsStore.GetTagAsync(tagId, cancellationToken);
            var decision = AccessDecisionService.Evaluate(readerEvent, registration);
            await operationsStore.RecordDecisionAsync(decision, cancellationToken);

            return Results.Created($"/api/v1/events/{decision.EventId:D}", decision);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> AcknowledgeOutboxAsync(
        Guid eventId,
        SqliteOperationsStore operationsStore,
        CancellationToken cancellationToken) =>
        await operationsStore.AcknowledgeOutboxAsync(eventId, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new { error = "No pending outbox event was found." });
}
