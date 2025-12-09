using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WScan2;

// Exposed so higher-level code can orchestrate tuning if needed.
public sealed class DvbFrontend : IDisposable
{
    private readonly SafeFileHandle _handle;
    private readonly string _path;

    public int Adapter { get; }
    public int FrontendIndex { get; }

    public DvbFrontend(int adapter = 0, int frontend = 0)
    {
        Adapter = adapter;
        FrontendIndex = frontend;
        _path = $"/dev/dvb/adapter{Adapter}/frontend{FrontendIndex}";
        _handle = File.OpenHandle(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
    }

    public bool Tune(int frequencyHz, int bandwidthHz, DeliverySystem system, TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        unsafe
        {
            var props = new[]
            {
                new DtvProperty { Cmd = DtvCommand.DTV_CLEAR },
                new DtvProperty { Cmd = DtvCommand.DTV_DELIVERY_SYSTEM, Data = (uint)system },
                new DtvProperty { Cmd = DtvCommand.DTV_FREQUENCY, Data = (uint)frequencyHz },
                new DtvProperty { Cmd = DtvCommand.DTV_BANDWIDTH_HZ, Data = (uint)bandwidthHz },
                new DtvProperty { Cmd = DtvCommand.DTV_TUNE },
            };

            fixed (DtvProperty* ptr = props)
            {
                var header = new DtvProperties
                {
                    Num = (uint)props.Length,
                    Props = (IntPtr)ptr,
                };

                var result = DvbInterop.ioctl(_handle, DvbInterop.FE_SET_PROPERTY, (IntPtr)(&header));
                if (result != 0)
                {
                    var errno = Marshal.GetLastPInvokeError();
                    throw new IOException($"ioctl FE_SET_PROPERTY failed (errno {errno}) for {_path}");
                }
            }
        }

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < lockTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = ReadStatus();
            if ((status & FeStatus.HasLock) != 0) return true;
            Thread.Sleep(50);
        }

        return false;
    }

    private FeStatus ReadStatus()
    {
        unsafe
        {
            uint status = 0;
            var ret = DvbInterop.ioctl(_handle, DvbInterop.FE_READ_STATUS, (IntPtr)(&status));
            if (ret != 0)
            {
                var errno = Marshal.GetLastPInvokeError();
                throw new IOException($"ioctl FE_READ_STATUS failed (errno {errno}) for {_path}");
            }

            return (FeStatus)status;
        }
    }

    public void Dispose()
    {
        _handle.Dispose();
    }
}
