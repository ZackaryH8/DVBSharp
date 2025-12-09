namespace DVBSharp.Tuner.Transmitters;

public class Transmitter
{
    public string SiteName { get; set; } = string.Empty;
    public string? Postcode { get; set; }
    public string? Region { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsRelay { get; set; }
    public List<MuxInfo> Muxes { get; set; } = new();
}
