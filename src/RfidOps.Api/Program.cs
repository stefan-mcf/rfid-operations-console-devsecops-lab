using System.Text.Json.Serialization;
using RfidOps.Api;
using RfidOps.Api.Storage;

if (await HealthCheckCommand.TryHandleAsync(args))
{
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<SqliteOperationsStore>();

var app = builder.Build();
var store = app.Services.GetRequiredService<SqliteOperationsStore>();
await store.InitializeAsync();

EndpointMappings.Map(app, builder.Configuration);
await app.RunAsync();
