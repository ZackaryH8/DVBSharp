using System.Collections.Concurrent;

namespace DVBSharp.Web;

/// <summary>
/// Tracks active client streams so that diagnostics can surface who is consuming which tuner.
/// </summary>
public sealed class ActiveStreamManager
{
    private readonly ConcurrentDictionary<Guid, ActiveStreamRecord> _streams = new();

    /// <summary>
    /// Starts tracking a stream for the provided tuner and returns the record for further inspection.
    /// </summary>
    public ActiveStreamRecord Start(string tunerId, int? frequency, string? label, string? client)
    {
        var record = new ActiveStreamRecord
        {
            TunerId = tunerId,
            Frequency = frequency,
            Label = label,
            Client = client
        };

        _streams[record.Id] = record;
        return record;
    }

    /// <summary>
    /// Stops tracking the stream with the provided ID if it still exists.
    /// </summary>
    public void End(Guid id)
    {
        _streams.TryRemove(id, out _);
    }

    /// <summary>
    /// Returns the currently active streams ordered by the time they started.
    /// </summary>
    public IReadOnlyCollection<ActiveStreamRecord> GetActive() =>
        _streams.Values
            .OrderByDescending(s => s.StartedAt)
            .ToList();
}
