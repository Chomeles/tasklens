using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TaskLens.App.Services.Interop;
using TaskLens.Core.Services;

namespace TaskLens.App.Services;

/// <summary>
/// <see cref="IProcessNetworkService"/> backed by a real-time ETW session on
/// Microsoft-Windows-Kernel-Network (tm2r-01): TCP/UDP send+receive events carry
/// <c>uint32 PID, uint32 size</c> at the start of their payload. Events are pumped by
/// <c>ProcessTrace</c> on a dedicated background thread into Core's
/// <see cref="ProcessNetworkAggregator"/> (unit-tested on Linux); each sample drains the
/// accumulated bytes into per-PID bytes/sec. Starting a trace session needs elevation —
/// without it <see cref="Availability"/> reports <c>RequiresAdmin</c> and every rate stays
/// an honest 0, like the real TM without a data source.
/// </summary>
internal sealed class EtwProcessNetworkService : IProcessNetworkService, IDisposable
{
    private const string SessionName = "Taskmanager2NetTrace";

    private static readonly Guid KernelNetworkProvider = new("7DD42A49-5329-4832-8DFD-43D979153A88");
    private const ulong KeywordTcpIpAndUdpIp = 0x30; // KERNEL_NETWORK_KEYWORD_IPV4/6 TCP 0x10 | UDP 0x20

    private static readonly IReadOnlyDictionary<int, double> Empty = new Dictionary<int, double>();

    private readonly ProcessNetworkAggregator aggregator = new();
    private readonly Stopwatch sinceLastSample = Stopwatch.StartNew();

    // Rooted for the native session lifetime: the callback thunk and the unmanaged buffers.
    private Advapi32.EventRecordCallback? callback;
    private nint properties;
    private nint loggerName;
    private ulong sessionHandle;
    private ulong traceHandle = Advapi32.InvalidProcessTraceHandle;
    private Thread? processThread;

    public EtwProcessNetworkService()
    {
        try
        {
            Availability = Start();
        }
        catch (Exception)
        {
            Availability = NetworkAttributionAvailability.Unavailable;
        }

        if (Availability != NetworkAttributionAvailability.Ok)
        {
            Dispose(); // release whatever partial state Start left behind
        }
    }

    public NetworkAttributionAvailability Availability { get; }

    public IReadOnlyDictionary<int, double> SampleNetworkBytesPerSecondByPid()
    {
        if (Availability != NetworkAttributionAvailability.Ok)
        {
            return Empty;
        }

        var elapsed = sinceLastSample.Elapsed;
        sinceLastSample.Restart();
        return aggregator.Tick(elapsed);
    }

    private NetworkAttributionAvailability Start()
    {
        var total = Marshal.SizeOf<Advapi32.EventTraceProperties>() + 2 * 1024; // room for the name tails
        properties = Marshal.AllocHGlobal(total);
        loggerName = Marshal.StringToHGlobalUni(SessionName);

        InitProperties(total);
        var status = Advapi32.StartTraceW(out sessionHandle, SessionName, properties);
        if (status == Advapi32.ErrorAlreadyExists)
        {
            // Orphaned session from a crashed/killed previous run — stop it by name, retry once.
            InitProperties(total);
            Advapi32.ControlTraceW(0, SessionName, properties, Advapi32.EventTraceControlStop);
            InitProperties(total);
            status = Advapi32.StartTraceW(out sessionHandle, SessionName, properties);
        }

        if (status == Advapi32.ErrorAccessDenied)
        {
            sessionHandle = 0;
            return NetworkAttributionAvailability.RequiresAdmin;
        }

        if (status != Advapi32.ErrorSuccess)
        {
            sessionHandle = 0;
            return NetworkAttributionAvailability.Unavailable;
        }

        status = Advapi32.EnableTraceEx2(
            sessionHandle,
            in KernelNetworkProvider,
            Advapi32.EventControlCodeEnableProvider,
            Advapi32.TraceLevelInformation,
            KeywordTcpIpAndUdpIp,
            matchAllKeyword: 0,
            timeout: 0,
            enableParameters: 0);
        if (status != Advapi32.ErrorSuccess)
        {
            return NetworkAttributionAvailability.Unavailable;
        }

        var recordCallback = new Advapi32.EventRecordCallback(OnEventRecord);
        callback = recordCallback; // rooted — the thunk must outlive the native session
        var logfile = new Advapi32.EventTraceLogFile
        {
            LoggerName = loggerName,
            ProcessTraceMode = Advapi32.ProcessTraceModeRealTime | Advapi32.ProcessTraceModeEventRecord,
            EventRecordCallbackPtr = Marshal.GetFunctionPointerForDelegate(recordCallback),
        };
        traceHandle = Advapi32.OpenTraceW(ref logfile);
        if (traceHandle == Advapi32.InvalidProcessTraceHandle)
        {
            return NetworkAttributionAvailability.Unavailable;
        }

        var handle = traceHandle;
        processThread = new Thread(() => Advapi32.ProcessTrace(ref handle, 1, 0, 0))
        {
            IsBackground = true,
            Name = "EtwNetTrace",
        };
        processThread.Start();
        return NetworkAttributionAvailability.Ok;
    }

    /// <summary>(Re)initializes the EVENT_TRACE_PROPERTIES buffer — required before every
    /// StartTraceW/ControlTraceW call because both write status fields back into it.</summary>
    private unsafe void InitProperties(int total)
    {
        new Span<byte>((void*)properties, total).Clear();
        ref var props = ref Unsafe.AsRef<Advapi32.EventTraceProperties>((void*)properties);
        props.Wnode.BufferSize = (uint)total;
        props.Wnode.Flags = Advapi32.WnodeFlagTracedGuid;
        props.Wnode.ClientContext = 1; // QPC timestamps
        props.LogFileMode = Advapi32.EventTraceRealTimeMode;
        props.FlushTimer = 1; // deliver at least once per second, matching the sampling tick
        props.LogFileNameOffset = 0; // real-time only, no file
        props.LoggerNameOffset = (uint)Marshal.SizeOf<Advapi32.EventTraceProperties>();
    }

    private void OnEventRecord(ref Advapi32.EventRecord record)
    {
        if (record.Header.ProviderId != KernelNetworkProvider)
        {
            return;
        }

        // TCPv4 send/recv 10/11, TCPv6 26/27, UDPv4 42/43, UDPv6 58/59 — payload starts with
        // uint32 PID, uint32 size for all of them.
        var id = record.Header.Descriptor.Id;
        if (id is not (10 or 11 or 26 or 27 or 42 or 43 or 58 or 59))
        {
            return;
        }

        if (record.UserDataLength < 8 || record.UserData == 0)
        {
            return; // defensive: never read past a truncated payload
        }

        var pid = Marshal.ReadInt32(record.UserData);
        var size = (uint)Marshal.ReadInt32(record.UserData, 4);
        aggregator.Add(pid, size);
    }

    public void Dispose()
    {
        if (traceHandle != Advapi32.InvalidProcessTraceHandle)
        {
            Advapi32.CloseTrace(traceHandle); // real-time: returns ERROR_CTX_CLOSE_PENDING, ends ProcessTrace
            traceHandle = Advapi32.InvalidProcessTraceHandle;
        }

        if (sessionHandle != 0)
        {
            InitProperties(Marshal.SizeOf<Advapi32.EventTraceProperties>() + 2 * 1024);
            Advapi32.ControlTraceW(sessionHandle, null, properties, Advapi32.EventTraceControlStop);
            sessionHandle = 0;
        }

        processThread?.Join(TimeSpan.FromSeconds(5));
        processThread = null;
        callback = null;

        if (properties != 0)
        {
            Marshal.FreeHGlobal(properties);
            properties = 0;
        }

        if (loggerName != 0)
        {
            Marshal.FreeHGlobal(loggerName);
            loggerName = 0;
        }
    }
}
