using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DVBSharp.Web.HdHomeRun;

public sealed class HdHomeRunSettingsStore
{
    private const int MaxAdvertisedTuners = 8;
    private readonly string _storagePath;
    private readonly ILogger<HdHomeRunSettingsStore> _logger;
    private readonly object _lock = new();
    private HdHomeRunSettings _settings;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public HdHomeRunSettingsStore(IWebHostEnvironment env, IOptions<HdHomeRunOptions> defaults, ILogger<HdHomeRunSettingsStore> logger)
    {
        _logger = logger;
        _storagePath = Path.Combine(env.ContentRootPath, "data", "hdhomerun_settings.json");
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _settings = Load(defaults.Value.TunerLimit);
    }

    public HdHomeRunSettings GetSettings()
    {
        lock (_lock)
        {
            return new HdHomeRunSettings
            {
                TunerLimit = _settings.TunerLimit
            };
        }
    }

    public async Task<HdHomeRunSettings> UpdateAsync(int? tunerLimit)
    {
        HdHomeRunSettings snapshot;
        lock (_lock)
        {
            _settings = new HdHomeRunSettings { TunerLimit = Normalize(tunerLimit) };
            snapshot = new HdHomeRunSettings { TunerLimit = _settings.TunerLimit };
        }

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        try
        {
            await File.WriteAllTextAsync(_storagePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist HDHomeRun settings to {Path}", _storagePath);
        }

        return snapshot;
    }

    public int ApplyLimit(int actualTuners)
    {
        var limit = _settings?.TunerLimit;
        if (limit.HasValue && limit.Value > 0)
        {
            return Math.Min(limit.Value, Math.Max(0, actualTuners));
        }

        return Math.Max(0, actualTuners);
    }

    private HdHomeRunSettings Load(int? defaultLimit)
    {
        if (File.Exists(_storagePath))
        {
            try
            {
                var json = File.ReadAllText(_storagePath);
                var parsed = JsonSerializer.Deserialize<HdHomeRunSettings>(json, JsonOptions);
                if (parsed != null)
                {
                    parsed.TunerLimit = Normalize(parsed.TunerLimit ?? defaultLimit);
                    return parsed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read HDHomeRun settings from {Path}", _storagePath);
            }
        }

        return new HdHomeRunSettings { TunerLimit = Normalize(defaultLimit) };
    }

    private static int? Normalize(int? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return null;
        }

        return Math.Min(MaxAdvertisedTuners, value.Value);
    }
}
