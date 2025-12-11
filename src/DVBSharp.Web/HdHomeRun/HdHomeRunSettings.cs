namespace DVBSharp.Web.HdHomeRun;

public sealed class HdHomeRunSettings
{
    /// <summary>
    /// Maximum number of tuners to advertise to HDHomeRun clients.
    /// Null indicates no limit and real tuner count should be used.
    /// </summary>
    public int? TunerLimit { get; set; }
}
