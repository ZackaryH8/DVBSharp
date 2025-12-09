using DVBSharp.Tuner.Models;
using Microsoft.Extensions.Logging;

namespace DVBSharp.Tuner;

public class FakeMuxScanner
{
    private readonly ILogger<FakeMuxScanner> _logger;
    private readonly MuxManager _muxManager;

    public FakeMuxScanner(MuxManager muxManager, ILogger<FakeMuxScanner> logger)
    {
        _muxManager = muxManager;
        _logger = logger;
    }

    public async Task<Mux> ScanAsync(int frequency)
    {
        ValidateFrequency(frequency);

        _logger.LogInformation("Starting fake mux scan at {Frequency} Hz", frequency);

        await Task.Delay(500); // simulate scan

        var mux = new Mux
        {
            Frequency = frequency,
            Bandwidth = 8_000_000,
            State = MuxState.Locked,
            Services = new List<Service>
            {
                new Service {
                    ServiceId = 101,
                    Name = "Fake News HD",
                    CallSign = "FAKENEWS",
                    LogicalChannelNumber = 1,
                    Category = "News",
                    PmtPid = 256,
                    VideoPids = { 512 },
                    AudioPids = { 660 },
                    Streams =
                    {
                        new StreamInfo { Type = "pat", Pid = 0, Codec = "PAT" },
                        new StreamInfo { Type = "pmt", Pid = 256, Codec = "MPEG-TS" },
                        new StreamInfo { Type = "video", Pid = 512, Codec = "H.264" },
                        new StreamInfo { Type = "audio", Pid = 660, Codec = "AAC" }
                    }
                },
                new Service {
                    ServiceId = 102,
                    Name = "Fake Sports 1",
                    CallSign = "FAKESPORTS1",
                    LogicalChannelNumber = 2,
                    Category = "Sports",
                    PmtPid = 257,
                    VideoPids = { 513 },
                    AudioPids = { 661 },
                    Streams =
                    {
                        new StreamInfo { Type = "pat", Pid = 0, Codec = "PAT" },
                        new StreamInfo { Type = "pmt", Pid = 257, Codec = "MPEG-TS" },
                        new StreamInfo { Type = "video", Pid = 513, Codec = "H.264" },
                        new StreamInfo { Type = "audio", Pid = 661, Codec = "AC-3" }
                    }
                }
            }
        };

        await _muxManager.UpsertAsync(mux);

        _logger.LogInformation("Fake scan completed for {Frequency} Hz with {ServiceCount} services", frequency, mux.Services.Count);

        return mux;
    }

    private static void ValidateFrequency(int frequency)
    {
        const int minFrequency = 47_000_000;   // 47 MHz lower UHF edge
        const int maxFrequency = 900_000_000;  // 900 MHz upper guard

        if (frequency < minFrequency || frequency > maxFrequency)
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), $"Frequency must be between {minFrequency} and {maxFrequency} Hz");
        }
    }
}
