namespace DVBSharp.Web.Requests;

public sealed class PinTunerRequest
{
    public string TunerId { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public string? Label { get; set; }
}
