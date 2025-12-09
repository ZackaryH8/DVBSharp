using System.Text.Json.Serialization;

namespace DVBSharp.Web.HdHomeRun;

public sealed class HdHomeRunLineupChannel
{
    public required string GuideNumber { get; init; }
    public required string GuideName { get; init; }
    public required string GuideId { get; init; }

    [JsonPropertyName("URL")]
    public required string Url { get; init; }

    public string? CallSign { get; init; }
    public string? Category { get; init; }
}
