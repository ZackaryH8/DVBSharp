namespace DVBSharp.Tuner.Models;

public class StreamInfo
{
    public string Type { get; set; } = "";
    public int Pid { get; set; }
    public string Codec { get; set; } = "";
}
