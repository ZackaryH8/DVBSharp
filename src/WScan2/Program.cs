using WScan2;

var options = ScanOptions.Parse(args);

Console.WriteLine($"[scan] Starting (adapter={options.Adapter}, frontend={options.Frontend}, bandwidth={options.BandwidthHz} Hz)");
var frequencies = DvbTScanner.LoadFrequencies(options.FrequenciesPath);
Console.WriteLine($"[scan] Frequencies to try: {frequencies.Count}");

var scanner = new DvbTScanner(
    options.Adapter,
    options.Frontend,
    options.BandwidthHz,
    TimeSpan.FromMilliseconds(options.LockTimeoutMs),
    TimeSpan.FromSeconds(options.ReadSeconds));

var muxes = new List<MuxInfo>();
foreach (var freq in frequencies)
{
    try
    {
        var mux = await scanner.ScanAsync(freq, CancellationToken.None);
        if (mux != null) muxes.Add(mux);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[scan] Error at {freq} Hz: {ex.Message}");
    }
}

DvbTScanner.SaveResults(options.OutputPath, muxes);
Console.WriteLine($"[scan] Done. Found {muxes.Sum(m => m.Services.Count)} services across {muxes.Count} muxes. Saved to {options.OutputPath}");

internal sealed record ScanOptions(
    int Adapter,
    int Frontend,
    int BandwidthHz,
    int LockTimeoutMs,
    int ReadSeconds,
    string? FrequenciesPath,
    string OutputPath)
{
    public static ScanOptions Parse(string[] args)
    {
        var adapter = 0;
        var frontend = 0;
        var bandwidth = 8_000_000;
        var lockTimeout = 2000;
        var readSeconds = 5;
        string? freqPath = null;
        var output = "scan.json";

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string? NextValue() => (i + 1) < args.Length ? args[++i] : null;

            switch (arg)
            {
                case "--adapter":
                    adapter = int.Parse(NextValue() ?? "0");
                    break;
                case "--frontend":
                    frontend = int.Parse(NextValue() ?? "0");
                    break;
                case "--bandwidth":
                    bandwidth = int.Parse(NextValue() ?? "8000000");
                    break;
                case "--lock-timeout-ms":
                    lockTimeout = int.Parse(NextValue() ?? "2000");
                    break;
                case "--read-seconds":
                    readSeconds = int.Parse(NextValue() ?? "5");
                    break;
                case "--frequencies":
                    freqPath = NextValue();
                    break;
                case "--output":
                    output = NextValue() ?? "scan.json";
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {arg}");
                    break;
            }
        }

        return new ScanOptions(adapter, frontend, bandwidth, lockTimeout, readSeconds, freqPath, output);
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
@"w_scan2-inspired DVB-T/T2 scanner (C#)
Options:
  --adapter <n>           DVB adapter index (/dev/dvb/adapterN), default 0
  --frontend <n>          Frontend index (/dev/dvb/adapterN/frontendM), default 0
  --bandwidth <hz>        Bandwidth in Hz, default 8000000
  --lock-timeout-ms <ms>  Lock timeout per tune, default 2000
  --read-seconds <s>      How long to read TS after lock, default 5
  --frequencies <path>    File with one frequency (Hz) per line; defaults to UHF 474-858 MHz grid
  --output <path>         JSON output file, default scan.json");
    }
}
