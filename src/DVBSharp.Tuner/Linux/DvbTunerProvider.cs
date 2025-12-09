using DVBSharp.Core;
using Microsoft.Extensions.Logging;

namespace DVBSharp.Tuner.Linux;

public sealed class DvbTunerProvider : ITunerProvider
{
    private readonly ILogger<DvbTunerProvider> _logger;
    private IReadOnlyList<ITuner>? _cachedTuners;
    private readonly object _sync = new();

    public DvbTunerProvider(ILogger<DvbTunerProvider> logger)
    {
        _logger = logger;
    }

    public IEnumerable<ITuner> CreateTuners()
    {
        if (_cachedTuners != null)
        {
            return _cachedTuners;
        }

        lock (_sync)
        {
            if (_cachedTuners == null)
            {
                _cachedTuners = DiscoverTuners().ToArray();
            }
        }

        return _cachedTuners;
    }

    private IEnumerable<ITuner> DiscoverTuners()
    {
        var adapters = DvbDeviceLocator.GetAdapters()
            .OrderBy(a => a.Adapter)
            .ToList();

        if (adapters.Count == 0)
        {
            _logger.LogWarning("No DVB adapters detected under /dev/dvb");
            yield break;
        }

        foreach (var adapter in adapters)
        {
            DvbTuner? tuner = null;
            try
            {
                tuner = new DvbTuner(adapter.Adapter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize tuner for adapter {Adapter}", adapter.Adapter);
            }

            if (tuner != null)
            {
                _logger.LogDebug("Discovered DVB tuner {Name} ({Id})", tuner.Info.Name, tuner.Id);
                yield return tuner;
            }
        }
    }
}
