using System.Collections.Concurrent;
using DVBSharp.Tuner.Models;
using Microsoft.Extensions.Logging;

namespace DVBSharp.Tuner;

public sealed class TunerManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ITuner> _tuners = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<TunerManager> _logger;

    public TunerManager(IEnumerable<ITunerProvider> providers, ILogger<TunerManager> logger)
    {
        _logger = logger;
        foreach (var provider in providers)
        {
            foreach (var tuner in provider.CreateTuners())
            {
                RegisterTuner(tuner);
            }
        }
    }

    public IEnumerable<TunerInfo> GetTuners() => _tuners.Values.Select(t => t.Info);

    public ITuner? GetTuner(string id) =>
        _tuners.TryGetValue(id, out var tuner) ? tuner : null;

    public bool RegisterTuner(ITuner tuner)
    {
        if (tuner == null) throw new ArgumentNullException(nameof(tuner));
        // Replace existing entry to allow runtime re-registration.
        var added = _tuners.TryAdd(tuner.Id, tuner);
        if (added)
        {
            _logger.LogInformation("Registered tuner {TunerName} ({TunerId})", tuner.Info.Name, tuner.Id);
        }
        else
        {
            _tuners[tuner.Id] = tuner;
            _logger.LogInformation("Re-registered tuner {TunerName} ({TunerId})", tuner.Info.Name, tuner.Id);
        }

        return added;
    }

    public async Task<IReadOnlyCollection<TunerSnapshot>> GetTunersWithStatusAsync()
    {
        var list = new List<TunerSnapshot>();
        foreach (var tuner in _tuners.Values)
        {
            var status = await TryGetStatusAsync(tuner);
            list.Add(new TunerSnapshot(tuner.Info, status));
        }

        return list;
    }

    public async Task<IReadOnlyCollection<TunerStatus>> GetStatusesAsync()
    {
        var statuses = new List<TunerStatus>();
        foreach (var tuner in _tuners.Values)
        {
            var status = await TryGetStatusAsync(tuner);
            if (status != null)
            {
                statuses.Add(status);
            }
        }

        return statuses;
    }

    public async Task<TunerStatus?> GetStatusAsync(string id)
    {
        var tuner = GetTuner(id);
        return tuner == null ? null : await TryGetStatusAsync(tuner);
    }

    private async Task<TunerStatus?> TryGetStatusAsync(ITuner tuner)
    {
        try
        {
            return await tuner.GetStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to query status for tuner {TunerId}", tuner.Id);
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var tuner in _tuners.Values)
        {
            if (tuner is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
