using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DVBSharp.Tuner.Linux;

internal static class DvbInterop
{
    // ioctl request codes (x86_64, from linux/dvb/frontend.h)
    public const uint FE_SET_PROPERTY = 0x40106f52;
    public const uint FE_GET_PROPERTY = 0x80106f53;
    public const uint FE_READ_STATUS = 0x80046f45;

    [DllImport("libc", SetLastError = true)]
    public static extern int ioctl(SafeFileHandle fd, uint request, IntPtr argp);
}

[Flags]
internal enum FeStatus : uint
{
    None = 0,
    HasSignal = 0x01,
    HasCarrier = 0x02,
    HasViterbi = 0x04,
    HasSync = 0x08,
    HasLock = 0x10,
    TimedOut = 0x20,
    Reinit = 0x40,
}

internal enum DeliverySystem : uint
{
    Undefined = 0,
    DVBC_AnnexA = 1,
    DVBC_AnnexB = 2,
    DVBT = 3,
    DVBS = 5,
    DVBS2 = 6,
    ISDBT = 8,
    ATSC = 11,
    DTMB = 13,
    DVBT2 = 16,
}

internal enum DtvCommand : uint
{
    DTV_UNDEFINED = 0,
    DTV_TUNE = 1,
    DTV_CLEAR = 2,
    DTV_FREQUENCY = 3,
    DTV_BANDWIDTH_HZ = 5,
    DTV_INVERSION = 6,
    DTV_DELIVERY_SYSTEM = 16,
}

[StructLayout(LayoutKind.Sequential)]
internal struct DtvProperty
{
    public DtvCommand Cmd;
    public uint Reserved1;
    public uint Reserved2;
    public uint Reserved3;
    public uint Data;
    public int Result;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DtvProperties
{
    public uint Num;
    public IntPtr Props;
}
