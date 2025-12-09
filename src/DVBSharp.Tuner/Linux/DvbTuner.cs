using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DVBSharp.Tuner.Models;
using Microsoft.Win32.SafeHandles;

namespace DVBSharp.Tuner.Linux;

/// <summary>
/// Minimal DVB-T/T2 tuner that talks directly to /dev/dvb/adapterX/*
/// </summary>
public sealed class DvbTuner : ITuner, IDisposable
{
    private readonly int _adapter;
    private readonly int _frontendIndex;
    private readonly string _frontendPath;
    private readonly string _dvrPath;
    private readonly int _bandwidthHz;
    private readonly DvbCapabilities _capabilities;
    private readonly SafeFileHandle _frontendHandle;

    private int _currentFrequency;
    private DeliverySystem _currentSystem = DeliverySystem.Undefined;
    private long _packetCount;

    public string Id => $"dvb-{_adapter}-{_frontendIndex}";
    public TunerInfo Info { get; }

    public DvbTuner(int adapter, int frontendIndex = 0, int bandwidthHz = 8_000_000)
    {
        _adapter = adapter;
        _frontendIndex = frontendIndex;
        _frontendPath = $"/dev/dvb/adapter{adapter}/frontend{frontendIndex}";
        _dvrPath = $"/dev/dvb/adapter{adapter}/dvr{frontendIndex}";
        _bandwidthHz = bandwidthHz;
        _capabilities = DvbCapabilities.Probe(adapter, frontendIndex);

        _frontendHandle = File.OpenHandle(_frontendPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        var capabilities = _capabilities.DeliverySystems
            .Select(ToFriendlyName)
            .ToArray();

        Info = new TunerInfo
        {
            Id = Id,
            Name = $"Tuner {adapter + 1}",
            Type = $"adapter{adapter}/frontend{frontendIndex}",
            Description = $"Frontend {_frontendPath} DVR {_dvrPath}",
            Capabilities = capabilities
        };
    }

    public async Task<bool> TuneAsync(int frequency)
    {
        // Try T2 first if supported, then T
        DeliverySystem system;
        if (_capabilities.Supports(DeliverySystem.DVBT2) &&
            await TryTuneAsync(frequency, DeliverySystem.DVBT2))
        {
            system = DeliverySystem.DVBT2;
        }
        else if (_capabilities.Supports(DeliverySystem.DVBT) &&
                 await TryTuneAsync(frequency, DeliverySystem.DVBT))
        {
            system = DeliverySystem.DVBT;
        }
        else
        {
            return false;
        }

        _currentFrequency = frequency;
        _currentSystem = system;
        return true;
    }

    private Task<bool> TryTuneAsync(int frequencyHz, DeliverySystem system)
    {
        return Task.Run(() =>
        {
            unsafe
            {
                var props = stackalloc DtvProperty[5];
                props[0] = new DtvProperty { Cmd = DtvCommand.DTV_CLEAR };
                props[1] = new DtvProperty { Cmd = DtvCommand.DTV_DELIVERY_SYSTEM, Data = (uint)system };
                props[2] = new DtvProperty { Cmd = DtvCommand.DTV_FREQUENCY, Data = (uint)frequencyHz };
                props[3] = new DtvProperty { Cmd = DtvCommand.DTV_BANDWIDTH_HZ, Data = (uint)_bandwidthHz };
                props[4] = new DtvProperty { Cmd = DtvCommand.DTV_TUNE };

                var header = new DtvProperties
                {
                    Num = 5,
                    Props = (IntPtr)props
                };

                var result = DvbInterop.ioctl(_frontendHandle, DvbInterop.FE_SET_PROPERTY, (IntPtr)(&header));
                if (result != 0)
                {
                    var errno = Marshal.GetLastPInvokeError();
                    Debug.WriteLine($"[dvb] tune ioctl failed errno={errno}");
                    return false;
                }
            }

            // Simple lock wait
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(2))
            {
                var status = ReadStatus();
                if ((status & FeStatus.HasLock) != 0) return true;
                Thread.Sleep(50);
            }

            return false;
        });
    }

    public async IAsyncEnumerable<byte[]> ReadStreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var fs = new FileStream(_dvrPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 188 * 256, useAsync: true);
        var buffer = new byte[188 * 256];

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0) yield break;

            // ensure whole packets
            var packets = read / 188;
            for (var i = 0; i < packets; i++)
            {
                var slice = new byte[188];
                Buffer.BlockCopy(buffer, i * 188, slice, 0, 188);
                _packetCount++;
                yield return slice;
            }
        }
    }

    public Task<TunerStatus> GetStatusAsync()
    {
        var status = ReadStatus();
        return Task.FromResult(new TunerStatus
        {
            TunerId = Id,
            Frequency = _currentFrequency,
            IsStreaming = (status & FeStatus.HasLock) != 0,
            PacketCount = _packetCount,
            BitrateBps = 0,
            LastUpdated = DateTimeOffset.UtcNow
        });
    }

    private FeStatus ReadStatus()
    {
        unsafe
        {
            uint val = 0;
            var ret = DvbInterop.ioctl(_frontendHandle, DvbInterop.FE_READ_STATUS, (IntPtr)(&val));
            if (ret != 0)
            {
                var errno = Marshal.GetLastPInvokeError();
                throw new IOException($"FE_READ_STATUS failed errno={errno} on {_frontendPath}");
            }

            return (FeStatus)val;
        }
    }

    public void Dispose()
    {
        _frontendHandle.Dispose();
    }

    private static string ToFriendlyName(DeliverySystem system) => system switch
    {
        DeliverySystem.DVBT => "DVB-T",
        DeliverySystem.DVBT2 => "DVB-T2",
        DeliverySystem.DVBC_AnnexA => "DVB-C (Annex A)",
        DeliverySystem.DVBC_AnnexB => "DVB-C (Annex B)",
        DeliverySystem.DVBS => "DVB-S",
        DeliverySystem.DVBS2 => "DVB-S2",
        DeliverySystem.ATSC => "ATSC",
        DeliverySystem.DTMB => "DTMB",
        DeliverySystem.ISDBT => "ISDB-T",
        _ => system.ToString()
    };
}
