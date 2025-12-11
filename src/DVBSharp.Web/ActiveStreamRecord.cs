namespace DVBSharp.Web;

/// <summary>
/// Immutable snapshot describing a single active or historical stream.
/// </summary>
public sealed class ActiveStreamRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TunerId { get; init; } = string.Empty;
    public int? Frequency { get; init; }
    public string? Label { get; init; }
    public string? Client { get; init; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
}
