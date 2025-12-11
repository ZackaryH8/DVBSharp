namespace DVBSharp.Web.PredefinedMuxes;

public sealed class PredefinedMuxDefinition
{
    public string Name { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public int BandwidthHz { get; set; }
    public string DeliverySystem { get; set; } = string.Empty;
    public string? Modulation { get; set; }
    public string? TransmissionMode { get; set; }
    public string? GuardInterval { get; set; }
    public string? CodeRateHp { get; set; }
    public string? CodeRateLp { get; set; }
    public string? StreamId { get; set; }
}
