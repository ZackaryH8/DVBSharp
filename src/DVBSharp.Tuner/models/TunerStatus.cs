namespace DVBSharp.Tuner.Models;

public class TunerStatus
{
    public string TunerId { get; set; } = "";
    public int Frequency { get; set; }
    public bool IsStreaming { get; set; }
    public long PacketCount { get; set; }
    public double BitrateBps { get; set; }
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
