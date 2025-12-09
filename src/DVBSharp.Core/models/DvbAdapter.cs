namespace DVBSharp.Core.Models;

public class DvbAdapter
{
    public int Adapter { get; set; }
    public string FrontendPath { get; set; } = "";
    public string DemuxPath { get; set; } = "";
    public string DvrPath { get; set; } = "";
}
