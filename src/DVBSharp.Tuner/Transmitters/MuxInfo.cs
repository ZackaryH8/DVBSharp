namespace DVBSharp.Tuner.Transmitters;

public class MuxInfo
{
    public string Name { get; set; } = string.Empty;
    public int? UhfChannel { get; set; }
    public double? FrequencyMHz { get; set; }
    public double? ErpKW { get; set; }
}
