namespace WScan2;

/// <summary>
/// Describes frontend capabilities derived from sysfs (delsys file).
/// </summary>
public sealed record DvbCapabilities(IReadOnlyList<DeliverySystem> DeliverySystems)
{
    public bool Supports(DeliverySystem system) => DeliverySystems.Contains(system);

    public static DvbCapabilities Probe(int adapter, int frontend)
    {
        var delsysPath = $"/sys/class/dvb/dvb{adapter}.frontend{frontend}/delsys";
        var systems = new List<DeliverySystem>();

        if (File.Exists(delsysPath))
        {
            foreach (var line in File.ReadAllLines(delsysPath))
            {
                if (TryParse(line.Trim(), out var sys))
                {
                    systems.Add(sys);
                }
            }
        }

        // Fallback: assume DVB-T/T2 if unknown.
        if (systems.Count == 0)
        {
            systems.Add(DeliverySystem.DVBT);
            systems.Add(DeliverySystem.DVBT2);
        }

        return new DvbCapabilities(systems);
    }

    private static bool TryParse(string value, out DeliverySystem system)
    {
        system = DeliverySystem.Undefined;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var up = value.ToUpperInvariant();
        if (up is "DVBT" or "DVB-T")
        {
            system = DeliverySystem.DVBT;
            return true;
        }
        if (up is "DVBT2" or "DVB-T2")
        {
            system = DeliverySystem.DVBT2;
            return true;
        }
        if (up is "DVBC/ANNEX_A" or "DVB-C/ANNEX_A")
        {
            system = DeliverySystem.DVBC_AnnexA;
            return true;
        }
        if (up is "DVBC/ANNEX_B" or "DVB-C/ANNEX_B")
        {
            system = DeliverySystem.DVBC_AnnexB;
            return true;
        }
        if (up is "DVBS" or "DVB-S")
        {
            system = DeliverySystem.DVBS;
            return true;
        }
        if (up is "DVBS2" or "DVB-S2")
        {
            system = DeliverySystem.DVBS2;
            return true;
        }
        if (up == "ATSC")
        {
            system = DeliverySystem.ATSC;
            return true;
        }
        if (up == "DTMB")
        {
            system = DeliverySystem.DTMB;
            return true;
        }
        if (up == "ISDBT")
        {
            system = DeliverySystem.ISDBT;
            return true;
        }

        return false;
    }
}
