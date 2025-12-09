namespace DVBSharp.Web.Requests;

public class NearestTransmitterRequest
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public class PostcodeLookupRequest
{
    public string Postcode { get; set; } = string.Empty;
}
