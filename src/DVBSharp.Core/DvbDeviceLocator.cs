using DVBSharp.Core.Models;

namespace DVBSharp.Core;

public static class DvbDeviceLocator
{
    public static List<DvbAdapter> GetAdapters()
    {
        var list = new List<DvbAdapter>();

        var basePath = "/dev/dvb";
        if (!Directory.Exists(basePath))
            return list;

        var adapters = Directory.GetDirectories(basePath);
        foreach (var adapterPath in adapters)
        {
            // Extract number: adapter0 -> 0
            var name = Path.GetFileName(adapterPath);
            if (!name.StartsWith("adapter")) continue;

            if (!int.TryParse(name.Substring(7), out int adapterNum))
                continue;

            var frontend = $"{adapterPath}/frontend0";
            var demux = $"{adapterPath}/demux0";
            var dvr = $"{adapterPath}/dvr0";

            list.Add(new DvbAdapter
            {
                Adapter = adapterNum,
                FrontendPath = frontend,
                DemuxPath = demux,
                DvrPath = dvr
            });
        }

        return list;
    }
}
