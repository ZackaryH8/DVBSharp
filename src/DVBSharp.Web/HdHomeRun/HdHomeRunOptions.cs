namespace DVBSharp.Web.HdHomeRun;

public sealed class HdHomeRunOptions
{
    public string DeviceId { get; set; } = "DVBSHARP";
    public string DeviceAuth { get; set; } = "dvbsharp";
    public string FriendlyName { get; set; } = "DVBSharp HDHomeRun";
    public string FirmwareName { get; set; } = "dvbsharp_atsc";
    public string FirmwareVersion { get; set; } = "2024.01.0";
    public string ModelNumber { get; set; } = "HDHR5-4DT";
    public string Manufacturer { get; set; } = "DVBSharp";
    public string SourceType { get; set; } = "Antenna";
    public int? TunerLimit { get; set; } = 4;
}
