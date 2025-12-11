using System.Globalization;
using System.IO.Compression;
using System.Net.Http;

namespace DVBSharp.Web.PredefinedMuxes;

public sealed class PredefinedMuxRepository
{
    private const string ArchiveUrl = "https://codeload.github.com/tvheadend/dtv-scan-tables/zip/refs/heads/tvheadend";
    private readonly ILogger<PredefinedMuxRepository> _logger;
    private readonly string _tablesDirectory;
    private readonly Lazy<IReadOnlyList<PredefinedMuxLocation>> _locations;

    public PredefinedMuxRepository(IWebHostEnvironment env, ILogger<PredefinedMuxRepository> logger)
    {
        _logger = logger;
        _tablesDirectory = Path.Combine(env.ContentRootPath, "data", "dtv-scan-tables", "dvb-t");
        Directory.CreateDirectory(_tablesDirectory);
        _locations = new Lazy<IReadOnlyList<PredefinedMuxLocation>>(LoadLocations, true);
    }

    public IReadOnlyCollection<PredefinedMuxLocation> GetLocations() => _locations.Value;

    public PredefinedMuxLocation? GetLocation(string id) =>
        _locations.Value.FirstOrDefault(loc => string.Equals(loc.Id, id, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<PredefinedMuxLocation> LoadLocations()
    {
        EnsureTablesPresent();

        if (!Directory.Exists(_tablesDirectory))
        {
            return Array.Empty<PredefinedMuxLocation>();
        }

        var files = Directory.EnumerateFiles(_tablesDirectory, "uk-*", SearchOption.TopDirectoryOnly);
        var locations = new List<PredefinedMuxLocation>();
        foreach (var file in files)
        {
            var parsed = ParseFile(file);
            if (parsed != null && parsed.Muxes.Count > 0)
            {
                locations.Add(parsed);
            }
        }

        return locations
            .OrderBy(loc => loc.Country)
            .ThenBy(loc => loc.Name)
            .ToList();
    }

    private void EnsureTablesPresent()
    {
        var existing = Directory.EnumerateFiles(_tablesDirectory, "uk-*", SearchOption.TopDirectoryOnly);
        if (existing.Any())
        {
            return;
        }

        try
        {
            DownloadTables();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download tvheadend scan tables. Predefined muxes will be unavailable.");
        }
    }

    private void DownloadTables()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            using (var http = new HttpClient())
            using (var response = http.GetAsync(ArchiveUrl).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                using var fs = File.Open(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                response.Content.CopyToAsync(fs).GetAwaiter().GetResult();
            }

            using var archiveStream = File.OpenRead(tempFile);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("/") || !entry.FullName.Contains("/dvb-t/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!entry.Name.StartsWith("uk-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var destination = Path.Combine(_tablesDirectory, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true);
            }
        }
        finally
        {
            try
            {
                File.Delete(tempFile);
            }
            catch
            {
                // ignored
            }
        }
    }

    private static PredefinedMuxLocation? ParseFile(string path)
    {
        var location = new PredefinedMuxLocation
        {
            Id = Path.GetFileName(path),
            Name = Path.GetFileName(path).Replace("uk-", "", StringComparison.OrdinalIgnoreCase),
        };

        var muxes = new List<PredefinedMuxDefinition>();
        PredefinedMuxDefinition? current = null;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                Commit();
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                ExtractMetadata(line, location);
                continue;
            }

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                Commit();
                current = new PredefinedMuxDefinition
                {
                    Name = line.Trim('[', ']')
                };
                continue;
            }

            if (current is null || !line.Contains('='))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim();
            var value = parts[1].Trim();

            switch (key)
            {
                case "DELIVERY_SYSTEM":
                    current.DeliverySystem = value;
                    break;
                case "FREQUENCY":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var freq))
                    {
                        current.Frequency = freq;
                    }
                    break;
                case "BANDWIDTH_HZ":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bw))
                    {
                        current.BandwidthHz = bw;
                    }
                    break;
                case "MODULATION":
                    current.Modulation = value;
                    break;
                case "TRANSMISSION_MODE":
                    current.TransmissionMode = value;
                    break;
                case "GUARD_INTERVAL":
                    current.GuardInterval = value;
                    break;
                case "CODE_RATE_HP":
                    current.CodeRateHp = value;
                    break;
                case "CODE_RATE_LP":
                    current.CodeRateLp = value;
                    break;
                case "STREAM_ID":
                    current.StreamId = value;
                    break;
            }
        }

        Commit();

        location.Muxes = muxes
            .Where(m => m.Frequency > 0)
            .OrderBy(m => m.Frequency)
            .ToList();
        if (string.IsNullOrWhiteSpace(location.Name) && location.Muxes.Count > 0)
        {
            location.Name = location.Muxes[0].Name;
        }

        return location.Muxes.Count == 0 ? null : location;

        void Commit()
        {
            if (current is null)
            {
                return;
            }

            muxes.Add(current);
            current = null;
        }
    }

    private static void ExtractMetadata(string line, PredefinedMuxLocation location)
    {
        if (line.Contains("location and provider", StringComparison.OrdinalIgnoreCase))
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                var segment = parts[1].Trim();
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    var segments = segment.Split(',', 2, StringSplitOptions.TrimEntries);
                    if (segments.Length == 2)
                    {
                        location.Country = segments[0];
                        location.Name = segments[1];
                    }
                    else
                    {
                        location.Name = segment;
                    }
                    location.Provider = segment;
                }
            }
        }
        else if (line.Contains("date", StringComparison.OrdinalIgnoreCase))
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2 &&
                DateTime.TryParse(parts[1].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
            {
                location.SourceDate = date;
            }
        }
    }
}
