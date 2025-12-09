namespace DVBSharp.Tuner.Models;

public class Service
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = "";
    public int PmtPid { get; set; }
    public List<int> AudioPids { get; set; } = new();
    public List<int> VideoPids { get; set; } = new();
    public List<StreamInfo> Streams { get; set; } = new();
    public int? LogicalChannelNumber { get; set; }
    public string? CallSign { get; set; }
    public string? Category { get; set; }
}
