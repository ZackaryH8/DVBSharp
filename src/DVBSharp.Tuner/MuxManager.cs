using System.Collections.Concurrent;
using System.Text.Json;
using DVBSharp.Tuner.Models;

namespace DVBSharp.Tuner;

public class MuxManager
{
    private readonly string _storagePath;
    private readonly ConcurrentDictionary<string, Mux> _muxes = new();

    public MuxManager(string storagePath)
    {
        _storagePath = storagePath;
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Load();
    }

    public IEnumerable<Mux> GetMuxes() => _muxes.Values.OrderBy(m => m.Frequency);

    public Mux? GetMux(string id) => _muxes.TryGetValue(id, out var mux) ? mux : null;

    public async Task<Mux> UpsertAsync(Mux mux)
    {
        mux.Id = GenerateId(mux.Frequency);

        if (_muxes.TryGetValue(mux.Id, out var existing))
        {
            mux = MergeMux(existing, mux);
        }

        mux.LastUpdated = DateTimeOffset.UtcNow;
        _muxes[mux.Id] = mux;
        await PersistAsync();
        return mux;
    }

    public IEnumerable<(Mux mux, Service service)> GetChannels()
    {
        foreach (var mux in _muxes.Values)
        {
            foreach (var service in mux.Services)
            {
                yield return (mux, service);
            }
        }
    }

    private void Load()
    {
        if (!File.Exists(_storagePath)) return;

        var json = File.ReadAllText(_storagePath);
        var muxes = JsonSerializer.Deserialize<List<Mux>>(json) ?? new List<Mux>();
        foreach (var mux in muxes)
        {
            mux.Id = GenerateId(mux.Frequency);
            _muxes[mux.Id] = mux;
        }
    }

    private async Task PersistAsync()
    {
        var json = JsonSerializer.Serialize(_muxes.Values, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_storagePath, json);
    }

    private static string GenerateId(int frequency) => $"mux-{frequency}";

    private static Mux MergeMux(Mux existing, Mux updated)
    {
        existing.Bandwidth = updated.Bandwidth;
        existing.State = updated.State;

        var serviceMap = existing.Services.ToDictionary(s => s.ServiceId);
        foreach (var svc in updated.Services)
        {
            serviceMap[svc.ServiceId] = svc;
        }

        existing.Services = serviceMap.Values.OrderBy(s => s.ServiceId).ToList();
        return existing;
    }
}
