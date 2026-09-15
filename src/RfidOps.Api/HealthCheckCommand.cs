namespace RfidOps.Api;

internal static class HealthCheckCommand
{
    public static async Task<bool> TryHandleAsync(string[] args)
    {
        if (!args.Contains("--health-check", StringComparer.Ordinal))
        {
            return false;
        }

        var healthUri = new UriBuilder(Uri.UriSchemeHttp, "127.0.0.1", 8080, "health").Uri;
        using var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        try
        {
            using var response = await healthClient.GetAsync(healthUri);
            Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException)
        {
            Environment.ExitCode = 1;
        }

        return true;
    }
}
