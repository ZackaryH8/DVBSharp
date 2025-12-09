using System.Text.Json;

namespace DVBSharp.Tuner.Transmitters;

public class TransmitterDatabase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public IReadOnlyList<Transmitter> Transmitters { get; }

    public TransmitterDatabase(string csvPath, string jsonPath)
    {
        // Print test
        Console.WriteLine("Initializing TransmitterDatabase...");
        var resolvedCsv = ResolvePath(csvPath);
        var resolvedJson = ResolvePath(jsonPath);

        // print resolved paths for debugging
        Console.WriteLine($"Resolved CSV Path: {resolvedCsv}");
        Console.WriteLine($"Resolved JSON Path: {resolvedJson}");

        // if (File.Exists(resolvedJson))
        // {
        //     Transmitters = LoadJson(resolvedJson);
        //     return;
        // }

        var parsed = UkTransmitterCsvImporter.ParseAsync(resolvedCsv).GetAwaiter().GetResult();
        // print parsed count for debugging
        Console.WriteLine($"Parsed {parsed.Count} transmitters from CSV.");
        var dir = Path.GetDirectoryName(resolvedJson);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        using (var fs = File.Create(resolvedJson))
        {
            JsonSerializer.Serialize(fs, parsed, JsonOptions);
        }

        Transmitters = LoadJson(resolvedJson);
    }

    public IEnumerable<(Transmitter Tx, double DistanceKm)> Nearest(double lat, double lon, int count = 5)
    {
        return Transmitters
            .Where(t => !double.IsNaN(t.Latitude) && !double.IsNaN(t.Longitude))
            .Select(t => (Tx: t, DistanceKm: DistanceKm(lat, lon, t.Latitude, t.Longitude)))
            .OrderBy(x => x.DistanceKm)
            .Take(count);
    }

    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6371;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(DegreesToRadians(lat1)) *
                Math.Cos(DegreesToRadians(lat2)) *
                Math.Pow(Math.Sin(dLon / 2), 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return radius * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static IReadOnlyList<Transmitter> LoadJson(string jsonPath)
    {
        using var fs = File.OpenRead(jsonPath);
        var data = JsonSerializer.Deserialize<List<Transmitter>>(fs, JsonOptions) ?? new List<Transmitter>();
        foreach (var tx in data)
        {
            tx.Muxes ??= new List<MuxInfo>();
        }

        return data;
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        var basePath = AppContext.BaseDirectory;
        return Path.Combine(basePath, path);
    }
}
