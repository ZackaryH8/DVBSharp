namespace DVBSharp.Web.PredefinedMuxes;

public sealed class PredefinedMuxLocation
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Provider { get; set; }
    public DateTime? SourceDate { get; set; }
    public IReadOnlyList<PredefinedMuxDefinition> Muxes { get; set; } = Array.Empty<PredefinedMuxDefinition>();
}
