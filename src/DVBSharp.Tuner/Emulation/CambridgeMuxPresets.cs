using System.Linq;
using DVBSharp.Tuner.Models;

namespace DVBSharp.Tuner.Emulation;

internal static class CambridgeMuxPresets
{
    private static readonly IReadOnlyDictionary<int, CambridgeMuxDefinition> _muxes = Build();

    public static IReadOnlyCollection<CambridgeMuxDefinition> All => _muxes.Values.ToList();

    public static bool TryResolve(int frequency, out CambridgeMuxDefinition? definition)
    {
        if (_muxes.TryGetValue(frequency, out definition))
        {
            return true;
        }

        const int toleranceHz = 250_000; // 250 kHz window
        definition = _muxes.Values.FirstOrDefault(m => Math.Abs(m.Frequency - frequency) <= toleranceHz);
        return definition is not null;
    }

    private static IReadOnlyDictionary<int, CambridgeMuxDefinition> Build()
    {
        var muxes = new[]
        {
            new CambridgeMuxDefinition(
                id: "sandy-heath-psb1",
                name: "PSB1 / BBC A",
                frequency: 474_000_000,
                bitrateBps: 24_130_000,
                services: new[]
                {
                    new CambridgeServiceDefinition(101, "BBC One East (E)", "BBCONEE", "Entertainment", 0x0064, 0x0200, 0x0300, 1),
                    new CambridgeServiceDefinition(102, "BBC Two", "BBCTWO", "Entertainment", 0x0065, 0x0201, 0x0301, 2),
                    new CambridgeServiceDefinition(103, "BBC Three", "BBCTHREE", "Entertainment", 0x0066, 0x0202, 0x0302, 23),
                    new CambridgeServiceDefinition(104, "BBC News", "BBCNEWS", "News", 0x0067, 0x0203, 0x0303, 231),
                    new CambridgeServiceDefinition(105, "CBBC", "CBBC", "Kids", 0x0068, 0x0204, 0x0304, 201)
                }),
            new CambridgeMuxDefinition(
                id: "sandy-heath-psb2",
                name: "PSB2 / D3&4",
                frequency: 498_000_000,
                bitrateBps: 24_130_000,
                services: new[]
                {
                    new CambridgeServiceDefinition(201, "ITV1 Anglia", "ITV1EAST", "Entertainment", 0x0101, 0x0300, 0x0400, 3),
                    new CambridgeServiceDefinition(202, "Channel 4", "C4UK", "Entertainment", 0x0102, 0x0301, 0x0401, 4),
                    new CambridgeServiceDefinition(203, "Channel 5", "C5UK", "Entertainment", 0x0103, 0x0302, 0x0402, 5),
                    new CambridgeServiceDefinition(204, "ITV2", "ITV2", "Entertainment", 0x0104, 0x0303, 0x0403, 6),
                    new CambridgeServiceDefinition(205, "E4", "E4", "Entertainment", 0x0105, 0x0304, 0x0404, 13)
                }),
            new CambridgeMuxDefinition(
                id: "sandy-heath-psb3",
                name: "PSB3 / BBC B HD",
                frequency: 522_000_000,
                bitrateBps: 40_200_000,
                services: new[]
                {
                    new CambridgeServiceDefinition(301, "BBC One HD", "BBCONEHD", "HD", 0x0201, 0x0500, 0x0600, 101),
                    new CambridgeServiceDefinition(302, "BBC Two HD", "BBCTWOHD", "HD", 0x0202, 0x0501, 0x0601, 102),
                    new CambridgeServiceDefinition(303, "ITV1 HD", "ITVHD", "HD", 0x0203, 0x0502, 0x0602, 103),
                    new CambridgeServiceDefinition(304, "Channel 4 HD", "C4HD", "HD", 0x0204, 0x0503, 0x0603, 104),
                    new CambridgeServiceDefinition(305, "BBC News HD", "BBCNEWSHD", "News", 0x0205, 0x0504, 0x0604, 107)
                }),
            new CambridgeMuxDefinition(
                id: "sandy-heath-com4",
                name: "COM4 / SDN",
                frequency: 514_000_000,
                bitrateBps: 27_100_000,
                services: new[]
                {
                    new CambridgeServiceDefinition(401, "ITV3", "ITV3", "Entertainment", 0x0301, 0x0700, 0x0800, 10),
                    new CambridgeServiceDefinition(402, "Drama", "DRAMA", "Entertainment", 0x0302, 0x0701, 0x0801, 20),
                    new CambridgeServiceDefinition(403, "Quest", "QUEST", "Entertainment", 0x0303, 0x0702, 0x0802, 12),
                    new CambridgeServiceDefinition(404, "5STAR", "5STAR", "Entertainment", 0x0304, 0x0703, 0x0803, 30),
                    new CambridgeServiceDefinition(405, "QVC", "QVC", "Shopping", 0x0305, 0x0704, 0x0804, 16)
                }),
            new CambridgeMuxDefinition(
                id: "sandy-heath-com5",
                name: "COM5 / ArqA",
                frequency: 530_000_000,
                bitrateBps: 27_100_000,
                services: new[]
                {
                    new CambridgeServiceDefinition(501, "Dave", "DAVE", "Entertainment", 0x0401, 0x0900, 0x0A00, 19),
                    new CambridgeServiceDefinition(502, "Sky Arts", "SKYARTS", "Culture", 0x0402, 0x0901, 0x0A01, 11),
                    new CambridgeServiceDefinition(503, "Film4+1", "FILM4P1", "Movies", 0x0403, 0x0902, 0x0A02, 45),
                    new CambridgeServiceDefinition(504, "Yesterday", "YESTERDAY", "History", 0x0404, 0x0903, 0x0A03, 27),
                    new CambridgeServiceDefinition(505, "Sky Mix", "SKYMIX", "Entertainment", 0x0405, 0x0904, 0x0A04, 31)
                }),
            new CambridgeMuxDefinition(
                id: "sandy-heath-com6",
                name: "COM6 / ArqB",
                frequency: 562_000_000,
                bitrateBps: 27_100_000,
                services: new[]
                {
                    new CambridgeServiceDefinition(601, "4Music", "4MUSIC", "Music", 0x0501, 0x0B00, 0x0C00, 30),
                    new CambridgeServiceDefinition(602, "PBS America", "PBS", "Documentary", 0x0502, 0x0B01, 0x0C01, 87),
                    new CambridgeServiceDefinition(603, "Forces TV", "FORCES", "News", 0x0503, 0x0B02, 0x0C02, 96),
                    new CambridgeServiceDefinition(604, "Al Jazeera Eng", "AJE", "News", 0x0504, 0x0B03, 0x0C03, 235),
                    new CambridgeServiceDefinition(605, "Together TV", "TOGETHER", "Lifestyle", 0x0505, 0x0B04, 0x0C04, 92)
                })
        };

        return muxes.ToDictionary(m => m.Frequency);
    }
}

internal sealed class CambridgeMuxDefinition
{
    public CambridgeMuxDefinition(string id, string name, int frequency, double bitrateBps, IReadOnlyList<CambridgeServiceDefinition> services)
    {
        Id = id;
        Name = name;
        Frequency = frequency;
        BitrateBps = bitrateBps;
        Services = services;
    }

    public string Id { get; }
    public string Name { get; }
    public int Frequency { get; }
    public double BitrateBps { get; }
    public IReadOnlyList<CambridgeServiceDefinition> Services { get; }

    public Mux ToMux()
    {
        return new Mux
        {
            Id = Id,
            Frequency = Frequency,
            Bandwidth = 8_000_000,
            State = MuxState.Locked,
            Services = Services.Select(s => s.ToService()).ToList()
        };
    }
}

internal sealed class CambridgeServiceDefinition
{
    public CambridgeServiceDefinition(int serviceId, string name, string callSign, string category, int pmtPid, int videoPid, int audioPid, int? logicalChannelNumber)
    {
        ServiceId = serviceId;
        Name = name;
        CallSign = callSign;
        Category = category;
        PmtPid = pmtPid;
        VideoPid = videoPid;
        AudioPid = audioPid;
        LogicalChannelNumber = logicalChannelNumber;
    }

    public int ServiceId { get; }
    public string Name { get; }
    public string CallSign { get; }
    public string Category { get; }
    public int PmtPid { get; }
    public int VideoPid { get; }
    public int AudioPid { get; }
    public int? LogicalChannelNumber { get; }

    public Service ToService()
    {
        var service = new Service
        {
            ServiceId = ServiceId,
            Name = Name,
            PmtPid = PmtPid,
            LogicalChannelNumber = LogicalChannelNumber,
            CallSign = CallSign,
            Category = Category,
            Streams = new List<StreamInfo>
            {
                new StreamInfo { Type = "pat", Pid = 0, Codec = "PAT" },
                new StreamInfo { Type = "pmt", Pid = PmtPid, Codec = "MPEG-TS" },
                new StreamInfo { Type = "video", Pid = VideoPid, Codec = "H.264" },
                new StreamInfo { Type = "audio", Pid = AudioPid, Codec = "AAC" }
            }
        };

        service.VideoPids.Add(VideoPid);
        service.AudioPids.Add(AudioPid);

        return service;
    }
}
