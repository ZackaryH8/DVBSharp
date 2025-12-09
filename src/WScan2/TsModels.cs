namespace WScan2;

// Expose scan results for reuse by other projects.
public record ElementaryStreamInfo(int Pid, byte StreamType, string Kind);

public record ServiceInfo(
    int ServiceId,
    string Name,
    int PmtPid,
    List<int> AudioPids,
    List<int> VideoPids,
    List<ElementaryStreamInfo> Streams);

public record MuxInfo(
    int FrequencyHz,
    string DeliverySystem,
    List<ServiceInfo> Services);
