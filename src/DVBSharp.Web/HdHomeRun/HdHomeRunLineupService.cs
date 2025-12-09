using System.Globalization;
using DVBSharp.Tuner;
using DVBSharp.Tuner.Models;
using Microsoft.Extensions.Logging;

namespace DVBSharp.Web.HdHomeRun;

public sealed class HdHomeRunLineupService
{
    private readonly MuxManager _muxManager;
    private readonly ILogger<HdHomeRunLineupService> _logger;

    public HdHomeRunLineupService(MuxManager muxManager, ILogger<HdHomeRunLineupService> logger)
    {
        _muxManager = muxManager;
        _logger = logger;
    }

    public IReadOnlyCollection<HdHomeRunLineupChannel> BuildLineup(string baseUrl, string? tunerId)
    {
        var channels = new List<HdHomeRunLineupChannel>();
        var fallbackNumber = 1;

        var ordered = _muxManager
            .GetChannels()
            .OrderBy(tuple => tuple.service.LogicalChannelNumber ?? int.MaxValue)
            .ThenBy(tuple => tuple.mux.Frequency)
            .ThenBy(tuple => tuple.service.ServiceId)
            .ToList();

        foreach (var (mux, service) in ordered)
        {
            var guideNumber = service.LogicalChannelNumber ?? fallbackNumber++;
            var identifier = $"{mux.Id}-{service.ServiceId}";
            var url = BuildStreamUrl(baseUrl, mux, service, tunerId);

            channels.Add(new HdHomeRunLineupChannel
            {
                GuideId = identifier,
                GuideName = service.Name,
                GuideNumber = guideNumber.ToString(CultureInfo.InvariantCulture),
                Url = url,
                CallSign = service.CallSign,
                Category = service.Category
            });
        }

        _logger.LogDebug("Generated HDHomeRun lineup with {ChannelCount} channels", channels.Count);

        return channels;
    }

    private static string BuildStreamUrl(string baseUrl, Mux mux, Service service, string? tunerId)
    {
        var tunerPath = string.IsNullOrWhiteSpace(tunerId) ? "any" : tunerId;
        var frequency = mux.Frequency.ToString(CultureInfo.InvariantCulture);
        return $"{baseUrl}/api/stream/{tunerPath}?frequency={frequency}&serviceId={service.ServiceId}";
    }
}
