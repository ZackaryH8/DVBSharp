namespace DVBSharp.Tuner.Models;

public class TunerInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Tuner";
    public string Type { get; set; } = "generic";
    public string? Description { get; set; }
    public string[] Capabilities { get; set; } = Array.Empty<string>();
}
