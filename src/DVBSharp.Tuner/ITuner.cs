using DVBSharp.Tuner.Models;

namespace DVBSharp.Tuner;

public interface ITuner
{
    string Id { get; }
    TunerInfo Info { get; }
    Task<bool> TuneAsync(int frequency);
    IAsyncEnumerable<byte[]> ReadStreamAsync(CancellationToken cancellationToken = default);
    Task<TunerStatus> GetStatusAsync();
}
