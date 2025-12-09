using System.Globalization;
using System.Text.Json;

namespace DVBSharp.Geo;

public class PostcodeLookup
{
    private static readonly HttpClient Http = CreateHttpClient();

    public async Task<(double lat, double lon)?> LookupAsync(string postcode)
    {
        if (string.IsNullOrWhiteSpace(postcode))
        {
            return null;
        }

        var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(postcode)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/json");
        var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var first = doc.RootElement[0];
        if (!first.TryGetProperty("lat", out var latEl) ||
            !first.TryGetProperty("lon", out var lonEl))
        {
            return null;
        }

        if (!TryParseCoordinate(latEl.GetString(), out var lat) ||
            !TryParseCoordinate(lonEl.GetString(), out var lon))
        {
            return null;
        }

        return (lat, lon);
    }

    private static bool TryParseCoordinate(string? value, out double coordinate)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DVBSharp/1.0 (https://github.com/zack/DVBSharp)");
        return client;
    }
}
