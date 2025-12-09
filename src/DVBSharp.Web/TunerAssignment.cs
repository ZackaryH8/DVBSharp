namespace DVBSharp.Web;

public sealed class TunerAssignment
{
    public string TunerId { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public string? Label { get; set; }
}
