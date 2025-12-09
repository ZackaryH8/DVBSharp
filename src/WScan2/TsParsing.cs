using System.Buffers.Binary;
using System.Text;

namespace WScan2;

internal sealed class PsiCollector
{
    private readonly Dictionary<int, SectionAssembler> _assemblers = new();
    private readonly Dictionary<int, int> _pat = new(); // serviceId -> pmtPid
    private readonly Dictionary<int, PmtPayload> _pmts = new();
    private readonly Dictionary<int, SdtService> _sdt = new();

    public bool HasPat => _pat.Count > 0;
    public bool HasSdt => _sdt.Count > 0;
    public bool IsComplete => HasPat && HasSdt && _pat.Values.All(pid => _pmts.ContainsKey(pid));

    public void AddData(ReadOnlySpan<byte> data)
    {
        var idx = 0;
        while (idx + 188 <= data.Length)
        {
            var packet = data.Slice(idx, 188);
            idx += 188;
            if (packet[0] != 0x47) continue;

            var pid = ((packet[1] & 0x1F) << 8) | packet[2];
            var payloadUnitStart = (packet[1] & 0x40) != 0;
            var adaptation = (packet[3] >> 4) & 0x03;
            var hasPayload = adaptation == 1 || adaptation == 3;
            var payloadIndex = 4;
            if (adaptation == 2) continue; // adaptation only
            if (adaptation == 3)
            {
                var adapLen = packet[4];
                payloadIndex = 5 + adapLen;
            }

            if (!hasPayload || payloadIndex >= 188) continue;
            var payload = packet.Slice(payloadIndex);

            if (!_assemblers.TryGetValue(pid, out var asm))
            {
                asm = new SectionAssembler(pid, OnSection);
                _assemblers[pid] = asm;
            }

            asm.AddPayload(payload, payloadUnitStart);
        }
    }

    private void OnSection(int pid, byte[] section)
    {
        if (section.Length < 3) return;
        var tableId = section[0];

        switch (tableId)
        {
            case 0x00 when pid == 0x0000:
                ParsePat(section);
                break;
            case 0x02:
                ParsePmt(pid, section);
                break;
            case 0x42: // SDT actual TS
            case 0x46: // SDT other TS
                if (pid == 0x0011) ParseSdt(section);
                break;
            default:
                break;
        }
    }

    private void ParsePat(ReadOnlySpan<byte> section)
    {
        if (section.Length < 8) return;
        var sectionLength = ((section[1] & 0x0F) << 8) | section[2];
        if (sectionLength + 3 > section.Length) return;

        var transportStreamId = BinaryPrimitives.ReadUInt16BigEndian(section.Slice(3, 2));
        _ = transportStreamId;

        var sectionNumber = section[6];
        var lastSectionNumber = section[7];
        _ = sectionNumber;
        _ = lastSectionNumber;

        var idx = 8;
        var end = 3 + sectionLength - 4; // minus CRC
        while (idx + 4 <= end)
        {
            var serviceId = BinaryPrimitives.ReadUInt16BigEndian(section.Slice(idx, 2));
            var pid = ((section[idx + 2] & 0x1F) << 8) | section[idx + 3];
            idx += 4;
            if (serviceId == 0) continue; // NIT
            _pat[serviceId] = pid;
        }
    }

    private void ParsePmt(int pid, ReadOnlySpan<byte> section)
    {
        if (section.Length < 12) return;
        var sectionLength = ((section[1] & 0x0F) << 8) | section[2];
        if (sectionLength + 3 > section.Length) return;

        var programNumber = BinaryPrimitives.ReadUInt16BigEndian(section.Slice(3, 2));
        var pcrPid = ((section[8] & 0x1F) << 8) | section[9];
        var programInfoLength = ((section[10] & 0x0F) << 8) | section[11];
        var idx = 12 + programInfoLength;
        var end = 3 + sectionLength - 4;

        var streams = new List<ElementaryStreamInfo>();
        var audioPids = new List<int>();
        var videoPids = new List<int>();

        while (idx + 5 <= end)
        {
            var streamType = section[idx];
            var esPid = ((section[idx + 1] & 0x1F) << 8) | section[idx + 2];
            var esInfoLength = ((section[idx + 3] & 0x0F) << 8) | section[idx + 4];
            idx += 5 + esInfoLength;

            var kind = StreamKind(streamType);
            streams.Add(new ElementaryStreamInfo(esPid, streamType, kind));
            if (kind == "video") videoPids.Add(esPid);
            if (kind == "audio") audioPids.Add(esPid);
        }

        _pmts[pid] = new PmtPayload(programNumber, pcrPid, streams, audioPids, videoPids);
    }

    private void ParseSdt(ReadOnlySpan<byte> section)
    {
        if (section.Length < 11) return;
        var sectionLength = ((section[1] & 0x0F) << 8) | section[2];
        if (sectionLength + 3 > section.Length) return;

        var idx = 11; // after original_network_id + last_section_number
        var end = 3 + sectionLength - 4;

        while (idx + 5 <= end)
        {
            var serviceId = BinaryPrimitives.ReadUInt16BigEndian(section.Slice(idx, 2));
            var descriptorsLoopLength = ((section[idx + 3] & 0x0F) << 8) | section[idx + 4];
            idx += 5;

            var name = $"Service {serviceId}";
            var descEnd = idx + descriptorsLoopLength;
            while (idx + 2 <= descEnd && descEnd <= section.Length)
            {
                var tag = section[idx];
                var len = section[idx + 1];
                var descData = section.Slice(idx + 2, Math.Min(len, descEnd - (idx + 2)));
                idx += 2 + len;

                if (tag == 0x48 && len >= 3) // service descriptor
                {
                    // descData[0] = service_type
                    var providerLen = descData[1];
                    var nameStart = 2 + providerLen;
                    if (nameStart < descData.Length)
                    {
                        var serviceNameLen = descData[nameStart];
                        var textStart = nameStart + 1;
                        if (textStart + serviceNameLen <= descData.Length)
                        {
                            name = DecodeDvbText(descData.Slice(textStart, serviceNameLen));
                        }
                    }
                }
            }

            _sdt[serviceId] = new SdtService(serviceId, name);
        }
    }

    public MuxInfo BuildMux(int frequencyHz, DeliverySystem system)
    {
        var services = new List<ServiceInfo>();

        foreach (var (serviceId, pmtPid) in _pat.OrderBy(k => k.Key))
        {
            if (!_pmts.TryGetValue(pmtPid, out var pmt)) continue;
            var name = _sdt.TryGetValue(serviceId, out var s) ? s.Name : $"Service {serviceId}";

            services.Add(new ServiceInfo(
                ServiceId: serviceId,
                Name: name,
                PmtPid: pmtPid,
                AudioPids: pmt.AudioPids,
                VideoPids: pmt.VideoPids,
                Streams: pmt.Streams));
        }

        return new MuxInfo(frequencyHz, system.ToString(), services);
    }

    private static string DecodeDvbText(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return string.Empty;
        try
        {
            // w_scan2 uses DVB text rules; we approximate with ISO-8859-1/UTF-8 fallback.
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return Encoding.Latin1.GetString(data);
        }
    }

    private static string StreamKind(byte streamType) =>
        streamType switch
        {
            0x01 or 0x02 or 0x10 or 0x1B or 0x24 or 0x2D or 0x2F => "video",
            0x03 or 0x04 or 0x0F or 0x11 or 0x1C or 0x2E => "audio",
            _ => "data",
        };

    private record PmtPayload(int ProgramNumber, int PcrPid, List<ElementaryStreamInfo> Streams, List<int> AudioPids, List<int> VideoPids);
    private record SdtService(int ServiceId, string Name);
}

internal sealed class SectionAssembler
{
    private readonly List<byte> _buffer = new();
    private readonly int _pid;
    private readonly Action<int, byte[]> _callback;

    public SectionAssembler(int pid, Action<int, byte[]> callback)
    {
        _pid = pid;
        _callback = callback;
    }

    public void AddPayload(ReadOnlySpan<byte> payload, bool payloadStart)
    {
        var idx = 0;
        if (payloadStart)
        {
            if (payload.Length == 0) return;
            var pointer = payload[0];
            idx = 1 + pointer;
            _buffer.Clear();
        }

        while (idx < payload.Length)
        {
            _buffer.Add(payload[idx]);
            idx++;

            if (_buffer.Count >= 3)
            {
                var sectionLength = ((_buffer[1] & 0x0F) << 8) | _buffer[2];
                var total = sectionLength + 3;
                if (_buffer.Count == total)
                {
                    _callback(_pid, _buffer.ToArray());
                    _buffer.Clear();
                    // Continue: there may be another section in this payload
                }
            }
        }
    }
}
