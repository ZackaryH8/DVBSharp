namespace DVBSharp.Tuner.Models;

public class Mux
{
    public string Id { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public int Bandwidth { get; set; } = 8000000;
    public MuxState State { get; set; } = MuxState.Unknown;
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    public List<Service> Services { get; set; } = new();
}
