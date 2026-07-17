using System.Runtime.InteropServices;

namespace TaskLens.App.Services.Interop;

/// <summary>
/// advapi32.dll ETW P/Invoke surface for <c>EtwProcessNetworkService</c>: a real-time trace
/// session on the Microsoft-Windows-Kernel-Network provider. <c>EVENT_TRACE_PROPERTIES</c> is
/// built manually in unmanaged memory (variable-length logger-name tail); the big structs the
/// service never reads (<c>EVENT_TRACE</c>, <c>TRACE_LOGFILE_HEADER</c>) are opaque fixed-size
/// blocks so the surrounding field offsets stay exact without modeling every union.
/// </summary>
internal static partial class Advapi32
{
    internal const uint ErrorSuccess = 0;
    internal const uint ErrorAccessDenied = 5;
    internal const uint ErrorAlreadyExists = 183;

    internal const uint WnodeFlagTracedGuid = 0x00020000;
    internal const uint EventTraceRealTimeMode = 0x00000100;
    internal const uint EventTraceControlStop = 1;
    internal const uint EventControlCodeEnableProvider = 1;
    internal const byte TraceLevelInformation = 4;

    internal const uint ProcessTraceModeRealTime = 0x00000100;
    internal const uint ProcessTraceModeEventRecord = 0x10000000;

    internal const ulong InvalidProcessTraceHandle = 0xFFFFFFFFFFFFFFFF;

    /// <summary>WNODE_HEADER — 48 bytes, leading member of EVENT_TRACE_PROPERTIES.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WnodeHeader
    {
        public uint BufferSize;
        public uint ProviderId;
        public ulong HistoricalContext;
        public long TimeStamp; // union with KernelHandle
        public Guid Guid;
        public uint ClientContext;
        public uint Flags;
    }

    /// <summary>EVENT_TRACE_PROPERTIES — allocated with extra tail room for the logger name.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct EventTraceProperties
    {
        public WnodeHeader Wnode;
        public uint BufferSize;
        public uint MinimumBuffers;
        public uint MaximumBuffers;
        public uint MaximumFileSize;
        public uint LogFileMode;
        public uint FlushTimer;
        public uint EnableFlags;
        public int AgeLimit;
        public uint NumberOfBuffers;
        public uint FreeBuffers;
        public uint EventsLost;
        public uint BuffersWritten;
        public uint LogBuffersLost;
        public uint RealTimeBuffersLost;
        public nint LoggerThreadId;
        public uint LogFileNameOffset;
        public uint LoggerNameOffset;
    }

    /// <summary>EVENT_DESCRIPTOR — 16 bytes.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct EventDescriptor
    {
        public ushort Id;
        public byte Version;
        public byte Channel;
        public byte Level;
        public byte Opcode;
        public ushort Task;
        public ulong Keyword;
    }

    /// <summary>EVENT_HEADER — 80 bytes.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct EventHeader
    {
        public ushort Size;
        public ushort HeaderType;
        public ushort Flags;
        public ushort EventProperty;
        public uint ThreadId;
        public uint ProcessId;
        public long TimeStamp;
        public Guid ProviderId;
        public EventDescriptor Descriptor;
        public ulong ProcessorTime; // union with KernelTime/UserTime
        public Guid ActivityId;
    }

    /// <summary>EVENT_RECORD as delivered to the event-record callback.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct EventRecord
    {
        public EventHeader Header;
        public uint BufferContext; // ETW_BUFFER_CONTEXT, 4 bytes, never read
        public ushort ExtendedDataCount;
        public ushort UserDataLength;
        public nint ExtendedData;
        public nint UserData;
        public nint UserContext;
    }

    /// <summary>PEVENT_RECORD_CALLBACK — <c>void WINAPI Callback(PEVENT_RECORD)</c>.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void EventRecordCallback(ref EventRecord record);

    /// <summary>
    /// EVENT_TRACE_LOGFILEW. <c>CurrentEvent</c> (EVENT_TRACE, 88 bytes on x64/arm64) and
    /// <c>LogfileHeader</c> (TRACE_LOGFILE_HEADER, 280 bytes) are output-only for real-time
    /// consumers and kept opaque; both start and end 8-aligned, so every modeled field keeps its
    /// native offset (ProcessTraceMode 28, BufferCallback 400, EventRecordCallback 424).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct EventTraceLogFile
    {
        public nint LogFileName;
        public nint LoggerName;
        public long CurrentTime;
        public uint BuffersRead;
        public uint ProcessTraceMode; // union with LogFileMode
        public fixed byte CurrentEvent[88];
        public fixed byte LogfileHeader[280];
        public nint BufferCallback;
        public uint BufferSize;
        public uint Filled;
        public uint EventsLost;
        public nint EventRecordCallbackPtr; // union with EventCallback
        public uint IsKernelTrace;
        public nint Context;
    }

    [LibraryImport("advapi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint StartTraceW(out ulong sessionHandle, string sessionName, nint properties);

    [LibraryImport("advapi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint ControlTraceW(ulong sessionHandle, string? sessionName, nint properties, uint controlCode);

    [LibraryImport("advapi32.dll")]
    internal static partial uint EnableTraceEx2(
        ulong sessionHandle,
        in Guid providerId,
        uint controlCode,
        byte level,
        ulong matchAnyKeyword,
        ulong matchAllKeyword,
        uint timeout,
        nint enableParameters);

    [LibraryImport("advapi32.dll")]
    internal static partial ulong OpenTraceW(ref EventTraceLogFile logfile);

    [LibraryImport("advapi32.dll")]
    internal static partial uint ProcessTrace(ref ulong handleArray, uint handleCount, nint startTime, nint endTime);

    [LibraryImport("advapi32.dll")]
    internal static partial uint CloseTrace(ulong traceHandle);
}
