using System.Runtime.CompilerServices;
using System.Text;
using DVBSharp.Tuner.Models;
using Microsoft.Extensions.Logging;

namespace DVBSharp.Tuner.Emulation;

internal sealed class FakeCambridgeTuner : ITuner
{
    private readonly MuxManager _muxManager;
    private readonly ILogger<FakeCambridgeTuner> _logger;
    private readonly TimeSpan _packetDelay = TimeSpan.FromMilliseconds(1.5);
    private readonly Random _random = new();

    private CambridgeMuxDefinition? _currentMux;
    private int _currentFrequency;
    private long _packetCount;
    private int _activeReaders;

    public FakeCambridgeTuner(MuxManager muxManager, ILogger<FakeCambridgeTuner> logger)
    {
        _muxManager = muxManager;
        _logger = logger;
        Info = new TunerInfo
        {
            Id = "fake-cambridge-1",
            Name = "Cambridgeshire Dev Tuner",
            Type = "virtual",
            Description = "Simulated Sandy Heath DVB-T/T2 front-end",
            Capabilities = new[] { "DVB-T", "DVB-T2" }
        };
    }

    public string Id => Info.Id;
    public TunerInfo Info { get; }

    public async Task<bool> TuneAsync(int frequency)
    {
        if (!CambridgeMuxPresets.TryResolve(frequency, out var definition) || definition is null)
        {
            _logger.LogWarning("Cambridge fake tuner: unsupported frequency {Frequency} Hz", frequency);
            _currentMux = null;
            _currentFrequency = 0;
            return false;
        }

        var mux = definition.ToMux();
        await _muxManager.UpsertAsync(mux);

        _currentMux = definition;
        _currentFrequency = definition.Frequency;
        Interlocked.Exchange(ref _packetCount, 0);

        _logger.LogInformation(
            "Cambridge fake tuner locked to {Mux} at {Frequency} Hz with {ServiceCount} services",
            definition.Name,
            definition.Frequency,
            definition.Services.Count);

        return true;
    }

    public async IAsyncEnumerable<byte[]> ReadStreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var mux = _currentMux;
        if (mux is null)
        {
            yield break;
        }

        Interlocked.Increment(ref _activeReaders);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var packet = BuildPacket(mux);
                Interlocked.Increment(ref _packetCount);
                yield return packet;
                await Task.Delay(_packetDelay, cancellationToken);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeReaders);
        }
    }

    public Task<TunerStatus> GetStatusAsync()
    {
        return Task.FromResult(new TunerStatus
        {
            TunerId = Id,
            Frequency = _currentFrequency,
            IsStreaming = Volatile.Read(ref _activeReaders) > 0,
            PacketCount = Interlocked.Read(ref _packetCount),
            BitrateBps = _currentMux?.BitrateBps ?? 0,
            LastUpdated = DateTimeOffset.UtcNow
        });
    }

    private byte[] BuildPacket(CambridgeMuxDefinition mux)
    {
        var service = mux.Services[_random.Next(mux.Services.Count)];
        var payload = new byte[188];
        payload[0] = 0x47; // sync byte

        var pid = service.VideoPid;
        payload[1] = (byte)(((pid >> 8) & 0x1F) | 0x40);
        payload[2] = (byte)(pid & 0xFF);
        payload[3] = 0x10; // payload only

        // Embed some pseudo content describing mux + service
        var nameBytes = Encoding.ASCII.GetBytes(service.CallSign ?? service.Name);
        Array.Copy(nameBytes, 0, payload, 4, Math.Min(nameBytes.Length, 16));

        BitConverter.TryWriteBytes(payload.AsSpan(24), mux.Frequency);
        BitConverter.TryWriteBytes(payload.AsSpan(32), service.ServiceId);

        for (var i = 40; i < payload.Length; i++)
        {
            payload[i] = (byte)_random.Next(0, 255);
        }

        return payload;
    }
}
