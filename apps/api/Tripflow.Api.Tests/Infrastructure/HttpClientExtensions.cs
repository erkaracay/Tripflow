using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Tripflow.Api.Tests.Infrastructure;

public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body, CancellationToken ct = default)
        => client.PostAsJsonAsync(url, body, JsonOptions, ct);

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response, CancellationToken ct = default)
    {
        var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    public static string? GetSetCookie(this HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return null;

        foreach (var value in values)
        {
            if (value.StartsWith(cookieName + "=", StringComparison.Ordinal))
                return value;
        }

        return null;
    }
}
