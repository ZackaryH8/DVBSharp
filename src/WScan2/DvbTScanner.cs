using System.Diagnostics;
using System.Text.Json;

namespace WScan2;

// Public so it can be reused from other projects in the solution.
public sealed class DvbTScanner
{
    private readonly int _adapter;
    private readonly int _frontend;
    private readonly int _bandwidthHz;
    private readonly TimeSpan _lockTimeout;
    private readonly TimeSpan _readDuration;
    private readonly DvbCapabilities _capabilities;

    public DvbTScanner(int adapter, int frontend, int bandwidthHz, TimeSpan lockTimeout, TimeSpan readDuration)
    {
        _adapter = adapter;
        _frontend = frontend;
        _bandwidthHz = bandwidthHz;
        _lockTimeout = lockTimeout;
        _readDuration = readDuration;
        _capabilities = DvbCapabilities.Probe(adapter, frontend);
        Console.WriteLine($"[capabilities] adapter={adapter}, frontend={frontend}: {string.Join(',', _capabilities.DeliverySystems)}");
    }

    public async Task<MuxInfo?> ScanAsync(int frequencyHz, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[scan] Tuning adapter {_adapter}, frontend {_frontend} to {frequencyHz} Hz (T2/T)");

        using var fe = new DvbFrontend(_adapter, _frontend);

        DeliverySystem systemUsed;
        if (_capabilities.Supports(DeliverySystem.DVBT2) &&
            fe.Tune(frequencyHz, _bandwidthHz, DeliverySystem.DVBT2, _lockTimeout, cancellationToken))
        {
            systemUsed = DeliverySystem.DVBT2;
        }
        else if (_capabilities.Supports(DeliverySystem.DVBT) &&
                 fe.Tune(frequencyHz, _bandwidthHz, DeliverySystem.DVBT, _lockTimeout, cancellationToken))
        {
            systemUsed = DeliverySystem.DVBT;
        }
        else
        {
            Console.WriteLine($"[scan] No lock at {frequencyHz} Hz (supported: {string.Join(',', _capabilities.DeliverySystems)})");
            return null;
        }

        Console.WriteLine($"[scan] Locked ({systemUsed}) @ {frequencyHz} Hz. Reading TS...");

        var dvrPath = $"/dev/dvb/adapter{_adapter}/dvr{_frontend}";
        using var dvr = new FileStream(dvrPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 188 * 1024, useAsync: true);

        var collector = new PsiCollector();
        var buffer = new byte[188 * 256];
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < _readDuration && !collector.IsComplete)
        {
            var read = await dvr.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0) break;
            collector.AddData(buffer.AsSpan(0, read));
        }

        var mux = collector.BuildMux(frequencyHz, systemUsed);
        Console.WriteLine($"[scan] {mux.Services.Count} services at {frequencyHz} Hz");
        return mux;
    }

    public static List<int> LoadFrequencies(string? freqFile)
    {
        if (freqFile != null && File.Exists(freqFile))
        {
            var list = new List<int>();
            foreach (var line in File.ReadAllLines(freqFile))
            {
                if (int.TryParse(line.Trim(), out var f)) list.Add(f);
            }
            if (list.Count > 0) return list;
        }

        // Default UHF DVB-T/T2 8MHz grid (Europe) 474-858 MHz.
        var freqs = new List<int>();
        for (var f = 474_000_000; f <= 858_000_000; f += 8_000_000)
        {
            freqs.Add(f);
        }
        return freqs;
    }

    public static void SaveResults(string path, IEnumerable<MuxInfo> muxes)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(muxes, options);
        File.WriteAllText(path, json);
    }
}
