using Microsoft.Extensions.Logging;

namespace DVBSharp.Tuner.Emulation;

public sealed class FakeCambridgeTunerProvider : ITunerProvider
{
    private readonly Lazy<IReadOnlyList<ITuner>> _tuners;

    public FakeCambridgeTunerProvider(MuxManager muxManager, ILoggerFactory loggerFactory)
    {
        _tuners = new Lazy<IReadOnlyList<ITuner>>(() =>
            new ITuner[] { new FakeCambridgeTuner(muxManager, loggerFactory.CreateLogger<FakeCambridgeTuner>()) });
    }

    public IEnumerable<ITuner> CreateTuners() => _tuners.Value;
}
