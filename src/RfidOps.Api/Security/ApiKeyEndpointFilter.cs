using System.Security.Cryptography;
using System.Text;

namespace RfidOps.Api.Security;

public sealed class ApiKeyEndpointFilter(
    string headerName,
    string configurationKey,
    IConfiguration configuration) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var expectedKey = configuration[configurationKey];
        if (string.IsNullOrWhiteSpace(expectedKey) || expectedKey.Length < 16)
        {
            return Results.Json(
                new { error = "This endpoint is unavailable until its API key is configured." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var expectedKeyBytes = Encoding.UTF8.GetBytes(expectedKey);
        var suppliedKey = context.HttpContext.Request.Headers[headerName].ToString();
        var suppliedKeyBytes = Encoding.UTF8.GetBytes(suppliedKey);

        var authorised = suppliedKeyBytes.Length == expectedKeyBytes.Length
            && CryptographicOperations.FixedTimeEquals(suppliedKeyBytes, expectedKeyBytes);

        if (!authorised)
        {
            return Results.Json(
                new { error = "A valid API key is required." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }
}
