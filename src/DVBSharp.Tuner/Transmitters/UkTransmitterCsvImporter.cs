using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;

namespace DVBSharp.Tuner.Transmitters;

internal static class UkTransmitterCsvImporter
{
    private static readonly string[] NameColumns = { "Site", "Site Name", "Name", "Station Name" };
    private static readonly string[] RegionColumns = { "Region", "Area" };
    private static readonly string[] PostcodeColumns = { "Postcode", "Post Code" };
    private static readonly string[] RelayColumns = { "Relay", "Is Relay", "Type" };
    private static readonly string[] LatitudeColumns = { "Latitude", "Lat" };
    private static readonly string[] LongitudeColumns = { "Longitude", "Lon", "Long" };
    private static readonly string[] EastingColumns = { "Easting", "OS Easting", "Eastings" };
    private static readonly string[] NorthingColumns = { "Northing", "OS Northing", "Northings" };
    private static readonly string[] GridRefColumns = { "NGR", "Grid Ref", "Grid Reference" };
    private static readonly string[] ChannelColumns = { "UHF Channel", "Channel" };
    private static readonly string[] FrequencyColumns = { "Frequency MHz", "Frequency", "Freq" };
    private static readonly string[] PowerColumns = { "Power", "ERP", "ERP (kW)" };
    private static readonly string[] CommentsColumns = { "Comments", "Mux" };
    private static readonly string[] KnownMuxes = { "PSB1", "PSB2", "PSB3", "COM4", "COM5", "COM6", "L-Mux" };
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.postcodes.io")
    };

    public static async Task<List<Transmitter>> ParseAsync(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Transmitter CSV not found at {csvPath}", csvPath);
        }

        var list = new List<Transmitter>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            DetectColumnCountChanges = false
        };

        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, config);
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        string? currentRegion = null;
        var postcodeCache = new Dictionary<string, string?>();

        while (csv.Read())
        {
            try
            {
                var row = ToDictionary(csv, headers);
                var siteName = GetValue(row, NameColumns);
                var gridRef = GetValue(row, GridRefColumns);

                if (string.IsNullOrWhiteSpace(gridRef))
                {
                    if (!string.IsNullOrWhiteSpace(siteName) && !HasAdditionalData(row, NameColumns))
                    {
                        currentRegion = siteName;
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(siteName))
                {
                    continue;
                }

                if (!TryResolveCoordinates(row, gridRef, out var latitude, out var longitude))
                {
                    continue;
                }

                var transmitter = new Transmitter
                {
                    SiteName = siteName!,
                    Postcode = GetValue(row, PostcodeColumns),
                    Region = GetValue(row, RegionColumns) ?? currentRegion,
                    Latitude = latitude,
                    Longitude = longitude,
                    IsRelay = ParseRelay(GetValue(row, RelayColumns) ?? GetValue(row, CommentsColumns)),
                    Muxes = ParseMuxes(row)
                };

                if (string.IsNullOrWhiteSpace(transmitter.Postcode))
                {
                    transmitter.Postcode = await ResolvePostcodeAsync(latitude, longitude, gridRef, postcodeCache);
                }

                transmitter.Postcode ??= gridRef;

                list.Add(transmitter);
            }
            catch
            {
                // skip malformed rows
            }
        }

        return list;
    }

    private static Dictionary<string, string?> ToDictionary(CsvReader csv, string[] headers)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            dict[header] = csv.GetField(header);
        }

        return dict;
    }

    private static bool TryResolveCoordinates(Dictionary<string, string?> row, string? gridRef, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;

        var latValue = GetValue(row, LatitudeColumns);
        var lonValue = GetValue(row, LongitudeColumns);
        if (TryDouble(latValue, out var lat) && TryDouble(lonValue, out var lon))
        {
            latitude = lat;
            longitude = lon;
            return true;
        }

        if (TryDouble(GetValue(row, EastingColumns), out var easting) &&
            TryDouble(GetValue(row, NorthingColumns), out var northing) &&
            OsgbConverter.TryOsGridToWgs84(easting, northing, out var latOs, out var lonOs))
        {
            latitude = latOs;
            longitude = lonOs;
            return true;
        }

        if (OsgbConverter.TryParseGridReference(gridRef, out var e, out var n) &&
            OsgbConverter.TryOsGridToWgs84(e, n, out var latGrid, out var lonGrid))
        {
            latitude = latGrid;
            longitude = lonGrid;
            return true;
        }

        return false;
    }

    private static List<MuxInfo> ParseMuxes(Dictionary<string, string?> row)
    {
        var muxes = new List<MuxInfo>();
        foreach (var mux in KnownMuxes)
        {
            var channel = TryParseInt(GetMuxField(row, mux, "UHF", "Channel", "Ch"));
            var frequency = TryParseDouble(GetMuxField(row, mux, "Freq", "Frequency"));
            var erp = TryParseDouble(GetMuxField(row, mux, "ERP"));

            if (channel == null && frequency == null && erp == null)
            {
                continue;
            }

            muxes.Add(new MuxInfo
            {
                Name = mux,
                UhfChannel = channel,
                FrequencyMHz = frequency,
                ErpKW = erp
            });
        }

        if (muxes.Count == 0)
        {
            var channel = TryParseInt(GetValue(row, ChannelColumns));
            var frequency = TryParseDouble(GetValue(row, FrequencyColumns));
            var erp = TryParseDouble(GetValue(row, PowerColumns));
            if (channel != null || frequency != null || erp != null)
            {
                muxes.Add(new MuxInfo
                {
                    Name = GetValue(row, CommentsColumns) ?? "UHF",
                    UhfChannel = channel,
                    FrequencyMHz = frequency,
                    ErpKW = erp
                });
            }
        }

        return muxes;
    }

    private static string? GetValue(Dictionary<string, string?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value!.Trim();
            }

            var matched = row.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (matched != null && row.TryGetValue(matched, out var alt) && !string.IsNullOrWhiteSpace(alt))
            {
                return alt!.Trim();
            }
        }

        return null;
    }

    private static bool HasAdditionalData(Dictionary<string, string?> row, params string[] excludeKeys)
    {
        var excluded = new HashSet<string>(excludeKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in row)
        {
            if (excluded.Contains(kvp.Key)) continue;
            if (!string.IsNullOrWhiteSpace(kvp.Value)) return true;
        }

        return false;
    }

    private static string? GetMuxField(Dictionary<string, string?> row, string muxName, params string[] hints)
    {
        foreach (var key in row.Keys)
        {
            if (!key.Contains(muxName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (hints.Any(h => key.Contains(h, StringComparison.OrdinalIgnoreCase)))
            {
                var value = row[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value!.Trim();
                }
            }
        }

        return null;
    }

    private static bool ParseRelay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return normalized.Equals("Relay", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("True", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("relay", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryDouble(string? input, out double value)
    {
        return double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static int? TryParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbl))
        {
            return (int)Math.Round(dbl);
        }

        return null;
    }

    private static double? TryParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
    private static async Task<string?> ResolvePostcodeAsync(double latitude, double longitude, string? fallbackKey, Dictionary<string, string?> cache)
    {
        var cacheKey = $"{latitude:F6},{longitude:F6}";
        if (cache.TryGetValue(cacheKey, out var cached))
        {
            return cached ?? fallbackKey;
        }

        try
        {
            var url = $"/postcodes?lon={longitude.ToString(CultureInfo.InvariantCulture)}&lat={latitude.ToString(CultureInfo.InvariantCulture)}&limit=1";
            using var response = await Http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                if (doc.RootElement.TryGetProperty("result", out var result) &&
                    result.ValueKind == JsonValueKind.Array &&
                    result.GetArrayLength() > 0)
                {
                    var postcode = result[0].GetProperty("postcode").GetString();
                    cache[cacheKey] = postcode;
                    return postcode ?? fallbackKey;
                }
            }
        }
        catch
        {
            // ignore network failures, fallback to grid ref
        }

        cache[cacheKey] = null;
        return fallbackKey;
    }
}
