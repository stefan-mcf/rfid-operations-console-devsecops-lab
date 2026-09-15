using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RfidOps.Core.Domain;

namespace RfidOps.Tests;

public sealed class ApiIntegrationTests(RfidOpsApiFactory factory) : IClassFixture<RfidOpsApiFactory>
{
    private const string ReaderKey = "reader-test-key-0001";
    private const string AdminKey = "admin-test-key-00001";
    private static readonly JsonSerializerOptions serializerOptions = CreateSerializerOptions();
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task HealthReportsReachableDatabase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var response = await client.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("healthy", body, StringComparison.Ordinal);
        Assert.Contains("reachable", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FaviconRequestDoesNotPolluteBrowserConsole()
    {
        var response = await client.GetAsync(
            "/favicon.ico",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UnknownTagEventIsDeniedAndPersisted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/events")
        {
            Content = JsonContent.Create(new
            {
                tagId = $"UNKNOWN-{Guid.NewGuid():N}",
                readerId = "DEMO-READER-01",
            }),
        };
        request.Headers.Add("X-Reader-Key", ReaderKey);

        var response = await client.SendAsync(request, cancellationToken);
        var decision = await response.Content.ReadFromJsonAsync<AccessDecision>(
            serializerOptions,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(decision);
        Assert.Equal(AccessOutcome.Denied, decision.Outcome);
        Assert.Equal(AccessReason.UnknownTag, decision.Reason);
    }

    [Fact]
    public async Task ActiveTagEventIsGranted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tagId = $"ACTIVE-{Guid.NewGuid():N}";
        using var tagRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/tags/{tagId}")
        {
            Content = JsonContent.Create(new
            {
                displayName = "Integration test tag",
                state = "Active",
            }),
        };
        tagRequest.Headers.Add("X-Admin-Key", AdminKey);
        var tagResponse = await client.SendAsync(tagRequest, cancellationToken);
        tagResponse.EnsureSuccessStatusCode();

        using var eventRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/events")
        {
            Content = JsonContent.Create(new
            {
                tagId,
                readerId = "DEMO-READER-01",
            }),
        };
        eventRequest.Headers.Add("X-Reader-Key", ReaderKey);
        var eventResponse = await client.SendAsync(eventRequest, cancellationToken);
        var decision = await eventResponse.Content.ReadFromJsonAsync<AccessDecision>(
            serializerOptions,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, eventResponse.StatusCode);
        Assert.NotNull(decision);
        Assert.Equal(AccessOutcome.Granted, decision.Outcome);
        Assert.Equal(AccessReason.ActiveRegistration, decision.Reason);
    }

    [Fact]
    public async Task MutationWithoutKeyIsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync("/api/v1/events", new
        {
            tagId = "DEMO-TAG-001",
            readerId = "DEMO-READER-01",
        }, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MetricsExposeDecisionAndOutboxSeries()
    {
        var metrics = await client.GetStringAsync(
            "/metrics",
            TestContext.Current.CancellationToken);

        Assert.Contains("rfid_ops_access_decisions_total", metrics, StringComparison.Ordinal);
        Assert.Contains("rfid_ops_outbox_pending", metrics, StringComparison.Ordinal);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class RfidOpsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        $"rfid-ops-api-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(directory);
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RFID_OPS_DB_PATH"] = Path.Combine(directory, "api-test.db"),
                ["RFID_OPS_READER_KEY"] = "reader-test-key-0001",
                ["RFID_OPS_ADMIN_KEY"] = "admin-test-key-00001",
            }));
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public new ValueTask DisposeAsync()
    {
        Dispose();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}
